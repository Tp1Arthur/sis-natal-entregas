// =====================================================
// Rastreio de presentes - Correio do Polo Norte
// Conversa com a API em C# (Program.cs) e desenha o mapa com Leaflet
// =====================================================

const API_URL = "http://localhost:5000";
const INTERVALO_MS = 5000;                      // de quanto em quanto tempo a tela se atualiza
const DURACAO_VOO_MS = 3000;                    // quanto tempo o trenó "voa" até a nova posição
const VILA_DO_PAPAI_NOEL = [66.5039, 25.7294];  // onde o trenó fica antes de partir

const ETAPAS = [
  { chave: "pendente",    titulo: "Carta recebida",       icone: "✉️" },
  { chave: "em_producao", titulo: "Presente em produção", icone: "🔨" },
  { chave: "a_caminho",   titulo: "Trenó a caminho",      icone: "🛷" },
  { chave: "entregue",    titulo: "Entregue",             icone: "🎁" },
];

const MENSAGENS = {
  pendente:    "Recebemos a sua carta! O duende já está cuidando do pedido.",
  em_producao: "Os duendes estão fabricando o seu presente na oficina.",
  a_caminho:   "O seu presente já está no trenó! Prepare o ouvido para os guizos.",
  entregue:    "Presente entregue! Feliz Natal! 🎄",
};

// ---------- Elementos da página ----------
const formBusca      = document.getElementById("form-busca");
const campoCodigo    = document.getElementById("codigo");
const caixaMensagem  = document.getElementById("mensagem");
const secaoResultado = document.getElementById("resultado");
const textoCrianca   = document.getElementById("info-crianca");
const textoBrinquedo = document.getElementById("info-brinquedo");
const textoEndereco  = document.getElementById("info-endereco");
const textoStatus    = document.getElementById("info-status");
const textoDistancia = document.getElementById("info-distancia");
const listaEtapas    = document.getElementById("linha-etapas");
const textoContagem  = document.getElementById("contagem");

// ---------- Estado ----------
let codigoAtual = null;
let temporizador = null;
let mapa = null;
let marcadorTreno = null;
let marcadorDestino = null;
let linhaRota = null;
let idAnimacao = null;

// =====================================================
// Busca
// =====================================================

formBusca.addEventListener("submit", (evento) => {
  evento.preventDefault();

  const codigo = campoCodigo.value.trim().toUpperCase();
  if (!codigo) {
    mostrarMensagem("Digite o código de rastreio do seu presente.");
    return;
  }

  campoCodigo.value = codigo;
  try {
    history.replaceState(null, "", `?codigo=${encodeURIComponent(codigo)}`);
  } catch {
    // se o navegador não permitir mexer no endereço, tudo bem
  }
  rastrear(codigo);
});

async function rastrear(codigo) {
  pararAtualizacao();
  codigoAtual = codigo;
  if (mapa) limparMapa();
  mostrarMensagem("Procurando o seu presente...");

  const continuar = await atualizar(true);

  // Enquanto o presente não foi entregue, a tela se atualiza sozinha
  if (continuar && codigoAtual === codigo) {
    temporizador = setInterval(async () => {
      const seguir = await atualizar(false);
      if (!seguir && codigoAtual === codigo) pararAtualizacao();
    }, INTERVALO_MS);
  }
}

function pararAtualizacao() {
  clearInterval(temporizador);
  temporizador = null;
}

// Busca os dados na API e atualiza a tela.
// Devolve true se vale a pena continuar atualizando, e false se pode parar.
async function atualizar(primeiraVez) {
  const codigo = codigoAtual;

  try {
    const [dados, posicao] = await Promise.all([
      buscarJson(`/api/rastreio/${encodeURIComponent(codigo)}`),
      buscarJson("/api/treno/posicao"),
    ]);

    if (codigo !== codigoAtual) return false; // o usuário já buscou outro código

    if (!dados) {
      secaoResultado.hidden = true;
      mostrarMensagem("Não encontramos esse código. Confira se digitou certinho (exemplo: NOEL-0004).");
      return false;
    }

    const posicaoTreno = posicao ? [posicao.latitude, posicao.longitude] : VILA_DO_PAPAI_NOEL;
    desenhar(dados, posicaoTreno);
    mostrarMensagem("");

    return dados.status !== "entregue";
  } catch (erro) {
    console.error(erro);
    if (codigo !== codigoAtual) return false;

    mostrarMensagem("Não consegui falar com o servidor do Papai Noel. Confira se a API está rodando.");
    if (primeiraVez) secaoResultado.hidden = true;
    return !primeiraVez; // se já estava funcionando, tenta de novo na próxima rodada
  }
}

// GET na API. Devolve o JSON, ou null se a API respondeu 404 (não encontrado).
async function buscarJson(caminho) {
  const resposta = await fetch(API_URL + caminho);
  if (resposta.status === 404) return null;
  if (!resposta.ok) throw new Error(`A API respondeu com erro ${resposta.status}`);
  return resposta.json();
}

function mostrarMensagem(texto) {
  caixaMensagem.textContent = texto;
  caixaMensagem.hidden = !texto;
}

// =====================================================
// Desenho da tela
// =====================================================

function desenhar(dados, posicaoTreno) {
  const destino = [dados.latitude, dados.longitude];

  // textContent (e não innerHTML) evita que um texto vindo da API vire HTML
  textoCrianca.textContent = dados.crianca;
  textoBrinquedo.textContent = dados.brinquedo;
  textoEndereco.textContent = `${dados.rua}, ${dados.numero} · ${dados.cidade}`;
  textoStatus.textContent = MENSAGENS[dados.status] ?? dados.status;

  const indiceAtual = Math.max(0, ETAPAS.findIndex((etapa) => etapa.chave === dados.status));
  desenharEtapas(indiceAtual);

  if (dados.status === "entregue") {
    textoDistancia.textContent = "Entrega concluída! 🎄";
  } else {
    const km = Math.round(distanciaKm(posicaoTreno, destino)).toLocaleString("pt-BR");
    textoDistancia.textContent = `O trenó está a ${km} km da sua casa`;
  }

  // O mapa só pode ser criado depois que a seção aparece na página
  secaoResultado.hidden = false;
  iniciarMapa();
  mapa.invalidateSize();

  if (!marcadorDestino) {
    marcadorDestino = L.marker(destino, { icon: criarIcone("🏠") }).addTo(mapa);
  }

  if (!marcadorTreno) {
    marcadorTreno = L.marker(posicaoTreno, { icon: criarIcone("🛷"), zIndexOffset: 1000 }).addTo(mapa);
    linhaRota = L.polyline([posicaoTreno, destino], {
      color: "#f4c542",
      weight: 3,
      dashArray: "8 10",
    }).addTo(mapa);
    mapa.fitBounds([posicaoTreno, destino], { padding: [60, 60] });
  } else {
    const atual = marcadorTreno.getLatLng();
    const mudou = Math.abs(atual.lat - posicaoTreno[0]) > 1e-6 || Math.abs(atual.lng - posicaoTreno[1]) > 1e-6;
    if (mudou) voarAte(posicaoTreno, destino);
  }
}

function desenharEtapas(indiceAtual) {
  listaEtapas.replaceChildren();

  ETAPAS.forEach((etapa, i) => {
    const item = document.createElement("li");
    item.className = "etapa" + (i <= indiceAtual ? " alcancada" : "") + (i === indiceAtual ? " atual" : "");

    const bolinha = document.createElement("span");
    bolinha.className = "bolinha";
    bolinha.textContent = etapa.icone;

    const titulo = document.createElement("span");
    titulo.textContent = etapa.titulo;

    item.append(bolinha, titulo);
    listaEtapas.append(item);
  });
}

// =====================================================
// Mapa
// =====================================================

function iniciarMapa() {
  if (mapa) return;

  mapa = L.map("mapa").setView([-15, -55], 4);
  L.tileLayer("https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png", {
    attribution:
      '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> ' +
      '&copy; <a href="https://carto.com/attributions">CARTO</a>',
    subdomains: "abcd",
    maxZoom: 19,
  }).addTo(mapa);
}

function criarIcone(emoji) {
  return L.divIcon({
    className: "icone-emoji",
    html: emoji,
    iconSize: [40, 40],
    iconAnchor: [20, 20],
  });
}

function limparMapa() {
  cancelAnimationFrame(idAnimacao);
  [marcadorTreno, marcadorDestino, linhaRota].forEach((camada) => {
    if (camada) mapa.removeLayer(camada);
  });
  marcadorTreno = null;
  marcadorDestino = null;
  linhaRota = null;
}

// O trenó "voa" do ponto onde está até a nova posição, em vez de dar um pulo
function voarAte(novaPosicao, destino) {
  cancelAnimationFrame(idAnimacao);

  const origem = marcadorTreno.getLatLng();
  const inicio = performance.now();

  function passo(agora) {
    const t = Math.min((agora - inicio) / DURACAO_VOO_MS, 1);
    const lat = origem.lat + (novaPosicao[0] - origem.lat) * t;
    const lng = origem.lng + (novaPosicao[1] - origem.lng) * t;

    marcadorTreno.setLatLng([lat, lng]);
    linhaRota.setLatLngs([[lat, lng], destino]);

    if (t < 1) idAnimacao = requestAnimationFrame(passo);
  }

  idAnimacao = requestAnimationFrame(passo);
}

// Distância em km entre dois pontos da Terra (fórmula de Haversine)
function distanciaKm([lat1, lng1], [lat2, lng2]) {
  const raioTerra = 6371;
  const rad = (grau) => (grau * Math.PI) / 180;

  const dLat = rad(lat2 - lat1);
  const dLng = rad(lng2 - lng1);
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(dLng / 2) ** 2;

  return 2 * raioTerra * Math.asin(Math.sqrt(a));
}

// =====================================================
// Enfeites: contagem regressiva e neve
// =====================================================

function diasParaNatal() {
  const hoje = new Date();
  const inicioHoje = new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate());
  let natal = new Date(hoje.getFullYear(), 11, 25);
  if (inicioHoje > natal) natal = new Date(hoje.getFullYear() + 1, 11, 25);
  return Math.round((natal - inicioHoje) / 86400000);
}

function mostrarContagem() {
  const dias = diasParaNatal();
  if (dias === 0) textoContagem.textContent = "🎅 Hoje é Natal! Feliz Natal!";
  else if (dias === 1) textoContagem.textContent = "🎄 Falta 1 dia para o Natal";
  else textoContagem.textContent = `🎄 Faltam ${dias} dias para o Natal`;
}

function criarNeve() {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

  const neve = document.getElementById("neve");
  for (let i = 0; i < 25; i++) {
    const floco = document.createElement("span");
    floco.className = "floco";
    floco.textContent = "❄";
    floco.style.left = Math.random() * 100 + "%";
    floco.style.fontSize = 10 + Math.random() * 14 + "px";
    floco.style.animationDuration = 8 + Math.random() * 10 + "s";
    floco.style.animationDelay = -Math.random() * 15 + "s";
    neve.append(floco);
  }
}

// =====================================================
// Início
// =====================================================

mostrarContagem();
criarNeve();

// Se o endereço já traz um código (ex.: rastreio.html?codigo=NOEL-0004), rastreia direto
const codigoDaUrl = new URLSearchParams(window.location.search).get("codigo");
if (codigoDaUrl) {
  campoCodigo.value = codigoDaUrl.toUpperCase();
  rastrear(codigoDaUrl.trim().toUpperCase());
}
