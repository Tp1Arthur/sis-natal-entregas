using Dapper;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// CORS: deixa o front (HTML/JS) acessar a API durante o desenvolvimento
builder.Services.AddCors(opcoes =>
    opcoes.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

var connString = app.Configuration.GetConnectionString("Natal")
    ?? throw new InvalidOperationException("Falta a connection string 'Natal' no appsettings.json");

var statusValidos = new[] { "pendente", "em_producao", "a_caminho", "entregue" };

// Teste rápido: a API está no ar?
app.MapGet("/", () => "API do Natal funcionando!");

// =====================================================
// ÁREA PÚBLICA: rastreio
// =====================================================

// Rastreio: GET /api/rastreio/NOEL-0004
app.MapGet("/api/rastreio/{codigo}", async (string codigo) =>
{
    const string sql = @"
        SELECT e.codigo_rastreio AS Codigo,
               e.status          AS Status,
               c.nome            AS Crianca,
               b.nome            AS Brinquedo,
               en.rua            AS Rua,
               en.numero         AS Numero,
               en.cidade         AS Cidade,
               en.ordem_rota     AS Parada,
               en.latitude       AS Latitude,
               en.longitude      AS Longitude
        FROM entrega e
        JOIN carta ca    ON ca.id = e.carta_id
        JOIN crianca c   ON c.id  = ca.crianca_id
        JOIN brinquedo b ON b.id  = ca.brinquedo_id
        JOIN endereco en ON en.id = c.endereco_id
        WHERE e.codigo_rastreio = @codigo";

    await using var conn = new MySqlConnection(connString);
    var resultado = await conn.QueryFirstOrDefaultAsync<Rastreio>(sql, new { codigo });

    return resultado is null
        ? Results.NotFound(new { erro = "Código de rastreio não encontrado." })
        : Results.Ok(resultado);
});

// Última posição do trenó: GET /api/treno/posicao
// (ordena pelo id, e não pela data, para a posição mais recente ser sempre a última gravada)
app.MapGet("/api/treno/posicao", async () =>
{
    const string sql = @"
        SELECT latitude  AS Latitude,
               longitude AS Longitude,
               data_hora AS DataHora
        FROM posicao_treno
        ORDER BY id DESC
        LIMIT 1";

    await using var conn = new MySqlConnection(connString);
    var posicao = await conn.QueryFirstOrDefaultAsync<PosicaoTreno>(sql);

    return posicao is null
        ? Results.NotFound(new { erro = "O trenó ainda não saiu." })
        : Results.Ok(posicao);
});

// =====================================================
// ÁREA DO DUENDE (administração)
// =====================================================

// Lista de brinquedos: GET /api/brinquedos
app.MapGet("/api/brinquedos", async () =>
{
    await using var conn = new MySqlConnection(connString);
    var lista = await conn.QueryAsync<Brinquedo>(
        "SELECT id AS Id, nome AS Nome, estoque AS Estoque FROM brinquedo ORDER BY nome");
    return Results.Ok(lista);
});

// Painel de entregas, na ordem da rota: GET /api/entregas
app.MapGet("/api/entregas", async () =>
{
    const string sql = @"
        SELECT e.id              AS Id,
               e.codigo_rastreio AS Codigo,
               e.status          AS Status,
               c.nome            AS Crianca,
               b.nome            AS Brinquedo,
               en.rua            AS Rua,
               en.numero         AS Numero,
               en.cidade         AS Cidade,
               en.ordem_rota     AS Parada
        FROM entrega e
        JOIN carta ca    ON ca.id = e.carta_id
        JOIN crianca c   ON c.id  = ca.crianca_id
        JOIN brinquedo b ON b.id  = ca.brinquedo_id
        JOIN endereco en ON en.id = c.endereco_id
        ORDER BY en.ordem_rota, e.id";

    await using var conn = new MySqlConnection(connString);
    var lista = await conn.QueryAsync<EntregaResumo>(sql);
    return Results.Ok(lista);
});

// Registrar uma carta: POST /api/cartas
// Cria (ou reaproveita) o endereço, cadastra a criança, a carta e a entrega com o código de rastreio.
app.MapPost("/api/cartas", async (NovaCarta nova) =>
{
    if (string.IsNullOrWhiteSpace(nova.Nome) || string.IsNullOrWhiteSpace(nova.Rua) ||
        string.IsNullOrWhiteSpace(nova.Numero) || string.IsNullOrWhiteSpace(nova.Cidade))
    {
        return Results.BadRequest(new { erro = "Informe nome, rua, número e cidade." });
    }

    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    var brinquedoExiste = await conn.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM brinquedo WHERE id = @BrinquedoId", new { nova.BrinquedoId });
    if (brinquedoExiste == 0)
    {
        return Results.BadRequest(new { erro = "Brinquedo não encontrado." });
    }

    // Tudo ou nada: se qualquer passo falhar, nada é gravado
    await using var tx = await conn.BeginTransactionAsync();

    // 1) Endereço: reaproveita se a casa já existe (assim duas crianças viram uma parada só)
    var enderecoId = await conn.ExecuteScalarAsync<int?>(
        "SELECT id FROM endereco WHERE rua = @Rua AND numero = @Numero AND cidade = @Cidade",
        new { nova.Rua, nova.Numero, nova.Cidade }, tx);

    if (enderecoId is null)
    {
        // Endereço novo vira a última parada da rota
        var proximaParada = await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(ordem_rota), 0) + 1 FROM endereco", transaction: tx);

        enderecoId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO endereco (rua, numero, bairro, cidade, latitude, longitude, ordem_rota)
            VALUES (@Rua, @Numero, @Bairro, @Cidade, @Latitude, @Longitude, @proximaParada);
            SELECT LAST_INSERT_ID();",
            new { nova.Rua, nova.Numero, nova.Bairro, nova.Cidade, nova.Latitude, nova.Longitude, proximaParada }, tx);
    }

    // 2) Criança
    var criancaId = await conn.ExecuteScalarAsync<int>(@"
        INSERT INTO crianca (nome, idade, endereco_id) VALUES (@Nome, @Idade, @enderecoId);
        SELECT LAST_INSERT_ID();",
        new { nova.Nome, nova.Idade, enderecoId }, tx);

    // 3) Carta
    var cartaId = await conn.ExecuteScalarAsync<int>(@"
        INSERT INTO carta (crianca_id, brinquedo_id, data_carta) VALUES (@criancaId, @BrinquedoId, CURDATE());
        SELECT LAST_INSERT_ID();",
        new { criancaId, nova.BrinquedoId }, tx);

    // 4) Entrega: entra com um código provisório e logo recebe o código final (NOEL-0009, NOEL-0010...)
    var entregaId = await conn.ExecuteScalarAsync<int>(@"
        INSERT INTO entrega (carta_id, codigo_rastreio, status) VALUES (@cartaId, @provisorio, 'pendente');
        SELECT LAST_INSERT_ID();",
        new { cartaId, provisorio = "TMP-" + Guid.NewGuid().ToString("N")[..16] }, tx);

    var codigo = $"NOEL-{entregaId:D4}";
    await conn.ExecuteAsync(
        "UPDATE entrega SET codigo_rastreio = @codigo WHERE id = @entregaId",
        new { codigo, entregaId }, tx);

    await tx.CommitAsync();

    return Results.Created($"/api/rastreio/{codigo}", new { codigo, status = "pendente" });
});

// Mudar o status de uma entrega: PUT /api/entregas/4/status  com  {"status":"entregue"}
app.MapPut("/api/entregas/{id:int}/status", async (int id, NovoStatus corpo) =>
{
    if (!statusValidos.Contains(corpo.Status))
    {
        return Results.BadRequest(new { erro = "Status inválido. Use: pendente, em_producao, a_caminho ou entregue." });
    }

    await using var conn = new MySqlConnection(connString);
    var linhas = await conn.ExecuteAsync(@"
        UPDATE entrega
        SET status = @Status,
            data_hora_entrega = CASE WHEN @Status = 'entregue' THEN NOW() ELSE NULL END
        WHERE id = @id",
        new { corpo.Status, id });

    return linhas == 0
        ? Results.NotFound(new { erro = "Entrega não encontrada." })
        : Results.Ok(new { id, status = corpo.Status });
});

// =====================================================
// ÁREA DO PAPAI NOEL (entregador)
// =====================================================

// A rota do dia, parada por parada: GET /api/rota
app.MapGet("/api/rota", async () =>
{
    const string sql = @"
        SELECT en.ordem_rota AS Parada,
               en.rua        AS Rua,
               en.numero     AS Numero,
               en.cidade     AS Cidade,
               en.latitude   AS Latitude,
               en.longitude  AS Longitude,
               COUNT(e.id)   AS Presentes,
               COUNT(CASE WHEN e.status = 'entregue' THEN 1 END) AS Entregues
        FROM endereco en
        LEFT JOIN crianca c ON c.endereco_id = en.id
        LEFT JOIN carta ca  ON ca.crianca_id = c.id
        LEFT JOIN entrega e ON e.carta_id    = ca.id
        GROUP BY en.id, en.ordem_rota, en.rua, en.numero, en.cidade, en.latitude, en.longitude
        ORDER BY en.ordem_rota";

    await using var conn = new MySqlConnection(connString);
    var rota = await conn.QueryAsync<ParadaRota>(sql);
    return Results.Ok(rota);
});

// Entregar todos os presentes de uma parada: POST /api/paradas/3/entregar
// O trenó vai para o endereço, os presentes da casa viram "entregue"
// e os da próxima parada que estavam "pendente" passam a "a_caminho".
app.MapPost("/api/paradas/{parada:int}/entregar", async (int parada) =>
{
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    var endereco = await conn.QueryFirstOrDefaultAsync<EnderecoParada>(
        "SELECT id AS Id, latitude AS Latitude, longitude AS Longitude FROM endereco WHERE ordem_rota = @parada",
        new { parada });

    if (endereco is null)
    {
        return Results.NotFound(new { erro = "Parada não encontrada." });
    }

    await using var tx = await conn.BeginTransactionAsync();

    // 1) Presentes da casa viram "entregue"
    var entregues = await conn.ExecuteAsync(@"
        UPDATE entrega e
        JOIN carta ca  ON ca.id = e.carta_id
        JOIN crianca c ON c.id  = ca.crianca_id
        SET e.status = 'entregue', e.data_hora_entrega = NOW()
        WHERE c.endereco_id = @Id AND e.status <> 'entregue'",
        new { endereco.Id }, tx);

    // 2) O trenó fica registrado nesse endereço
    await conn.ExecuteAsync(@"
        INSERT INTO posicao_treno (latitude, longitude, data_hora, endereco_id)
        VALUES (@Latitude, @Longitude, NOW(), @Id)",
        new { endereco.Latitude, endereco.Longitude, endereco.Id }, tx);

    // 3) Presentes da próxima parada que estavam "pendente" passam a "a_caminho"
    await conn.ExecuteAsync(@"
        UPDATE entrega e
        JOIN carta ca    ON ca.id = e.carta_id
        JOIN crianca c   ON c.id  = ca.crianca_id
        JOIN endereco en ON en.id = c.endereco_id
        SET e.status = 'a_caminho'
        WHERE en.ordem_rota = @proxima AND e.status = 'pendente'",
        new { proxima = parada + 1 }, tx);

    await tx.CommitAsync();

    return Results.Ok(new { parada, presentesEntregues = entregues });
});

// Registrar uma posição do trenó (ex.: em voo entre duas paradas): POST /api/treno/posicao
app.MapPost("/api/treno/posicao", async (NovaPosicao nova) =>
{
    await using var conn = new MySqlConnection(connString);
    await conn.ExecuteAsync(@"
        INSERT INTO posicao_treno (latitude, longitude, data_hora, endereco_id)
        VALUES (@Latitude, @Longitude, NOW(), NULL)",
        new { nova.Latitude, nova.Longitude });

    return Results.Ok(new { nova.Latitude, nova.Longitude });
});

app.Run();

// Formatos dos dados (os nomes batem com os apelidos do SQL e com o JSON enviado)
record Rastreio(string Codigo, string Status, string Crianca, string Brinquedo,
                string Rua, string Numero, string Cidade, int Parada,
                decimal Latitude, decimal Longitude);

record PosicaoTreno(decimal Latitude, decimal Longitude, DateTime DataHora);

record Brinquedo(int Id, string Nome, int Estoque);

record EntregaResumo(int Id, string Codigo, string Status, string Crianca, string Brinquedo,
                     string Rua, string Numero, string Cidade, int Parada);

record NovaCarta(string Nome, int? Idade, string Rua, string Numero, string Bairro, string Cidade,
                 decimal Latitude, decimal Longitude, int BrinquedoId);

record NovoStatus(string Status);

record ParadaRota(int Parada, string Rua, string Numero, string Cidade,
                  decimal Latitude, decimal Longitude, long Presentes, long Entregues);

record EnderecoParada(int Id, decimal Latitude, decimal Longitude);

record NovaPosicao(decimal Latitude, decimal Longitude);
