-- =====================================================
-- Natal Entregas - Estrutura do banco de dados
-- Uso: mysql -u root -p NOME_DO_BANCO < banco/01_estrutura.sql
-- Ordem: primeiro as tabelas sem dependência, depois as com FK
-- =====================================================

-- 1) Sem dependências ------------------------------------

CREATE TABLE IF NOT EXISTS endereco (
  id          INT NOT NULL AUTO_INCREMENT,
  rua         VARCHAR(120) NOT NULL,
  numero      VARCHAR(10)  NOT NULL,
  bairro      VARCHAR(80)  NOT NULL,
  cidade      VARCHAR(80)  NOT NULL,
  latitude    DECIMAL(9,6) NOT NULL,
  longitude   DECIMAL(9,6) NOT NULL,
  ordem_rota  INT NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_endereco_ordem_rota (ordem_rota)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS brinquedo (
  id       INT NOT NULL AUTO_INCREMENT,
  nome     VARCHAR(100) NOT NULL,
  estoque  INT NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  CONSTRAINT ck_brinquedo_estoque CHECK (estoque >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2) Dependem de endereco / brinquedo --------------------

CREATE TABLE IF NOT EXISTS crianca (
  id           INT NOT NULL AUTO_INCREMENT,
  nome         VARCHAR(100) NOT NULL,
  idade        TINYINT UNSIGNED DEFAULT NULL,
  endereco_id  INT NOT NULL,
  PRIMARY KEY (id),
  KEY idx_crianca_endereco (endereco_id),
  CONSTRAINT fk_crianca_endereco FOREIGN KEY (endereco_id) REFERENCES endereco (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3) Dependem de crianca ---------------------------------

CREATE TABLE IF NOT EXISTS carta (
  id            INT NOT NULL AUTO_INCREMENT,
  crianca_id    INT NOT NULL,
  brinquedo_id  INT NOT NULL,
  data_carta    DATE NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_carta_crianca (crianca_id),
  KEY idx_carta_brinquedo (brinquedo_id),
  CONSTRAINT fk_carta_crianca   FOREIGN KEY (crianca_id)   REFERENCES crianca (id),
  CONSTRAINT fk_carta_brinquedo FOREIGN KEY (brinquedo_id) REFERENCES brinquedo (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4) Dependem de carta -----------------------------------

CREATE TABLE IF NOT EXISTS entrega (
  id                 INT NOT NULL AUTO_INCREMENT,
  carta_id           INT NOT NULL,
  codigo_rastreio    VARCHAR(20) NOT NULL,
  status             ENUM('pendente','em_producao','a_caminho','entregue') NOT NULL DEFAULT 'pendente',
  data_hora_entrega  DATETIME DEFAULT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_entrega_carta (carta_id),
  UNIQUE KEY uq_entrega_codigo (codigo_rastreio),
  CONSTRAINT fk_entrega_carta FOREIGN KEY (carta_id) REFERENCES carta (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5) Posição do trenó (NULL em endereco_id = em trânsito) -

CREATE TABLE IF NOT EXISTS posicao_treno (
  id           INT NOT NULL AUTO_INCREMENT,
  latitude     DECIMAL(9,6) NOT NULL,
  longitude    DECIMAL(9,6) NOT NULL,
  data_hora    DATETIME NOT NULL,
  endereco_id  INT DEFAULT NULL,
  PRIMARY KEY (id),
  KEY idx_posicao_data_hora (data_hora),
  KEY idx_posicao_endereco (endereco_id),
  CONSTRAINT fk_posicao_endereco FOREIGN KEY (endereco_id) REFERENCES endereco (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
