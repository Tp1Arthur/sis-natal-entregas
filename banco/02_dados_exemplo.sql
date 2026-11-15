-- Natal Entregas - Dados de exemplo (todos fictícios)
-- Uso: mysql -u root -p NOME_DO_BANCO < banco/02_dados_exemplo.sql
-- Carregar DEPOIS de 01_estrutura.sql

INSERT INTO `endereco` VALUES (1,'Rua das Estrelas','101','Centro','Porto Velho',-8.761200,-63.903900,1),(2,'Avenida do Trenó','202','Centro','Manaus',-3.119000,-60.021700,2),(3,'Rua dos Duendes','303','Asa Sul','Brasília',-15.793900,-47.882800,3),(4,'Alameda dos Flocos','404','Centro','Rio de Janeiro',-22.906800,-43.172900,4),(5,'Rua da Neve','505','Vila Mariana','São Paulo',-23.550500,-46.633300,5),(6,'Travessa do Pinheiro','606','Batel','Curitiba',-25.428400,-49.273300,6);
INSERT INTO `brinquedo` VALUES (1,'Bicicleta',5),(2,'Boneca',10),(3,'Carrinho de controle remoto',8),(4,'Castelo de blocos de montar',12),(5,'Bola de futebol',15),(6,'Quebra-cabeça',20);
INSERT INTO `crianca` VALUES (1,'Lucas',7,1),(2,'Maria',5,1),(3,'Pedro',9,2),(4,'Ana',NULL,3),(5,'João',6,4),(6,'Beatriz',8,4),(7,'Sofia',4,5),(8,'Miguel',10,6),(9,'Clara',6,6);
INSERT INTO `carta` VALUES (1,1,3,'2026-11-20'),(2,2,2,'2026-11-20'),(3,3,1,'2026-11-22'),(4,4,4,'2026-11-25'),(5,5,5,'2026-11-26'),(6,6,6,'2026-11-26'),(7,7,2,'2026-11-28'),(8,8,4,'2026-11-30'),(9,9,2,'2026-11-14');
INSERT INTO `entrega` VALUES (1,1,'NOEL-0001','entregue','2026-12-24 22:05:00'),(2,2,'NOEL-0002','entregue','2026-12-24 22:05:00'),(3,3,'NOEL-0003','entregue','2026-12-24 23:05:00'),(4,4,'NOEL-0004','entregue','2026-11-14 22:23:39'),(5,5,'NOEL-0005','a_caminho',NULL),(6,6,'NOEL-0006','a_caminho',NULL),(7,7,'NOEL-0007','pendente',NULL),(8,8,'NOEL-0008','em_producao',NULL),(9,9,'NOEL-0009','pendente',NULL);
INSERT INTO `posicao_treno` VALUES (1,-8.761200,-63.903900,'2026-12-24 22:00:00',1),(2,-5.940000,-62.000000,'2026-12-24 22:30:00',NULL),(3,-3.119000,-60.021700,'2026-12-24 23:00:00',2),(4,-9.500000,-54.000000,'2026-12-24 23:30:00',NULL),(5,-15.793900,-47.882800,'2026-11-14 22:23:39',3);
