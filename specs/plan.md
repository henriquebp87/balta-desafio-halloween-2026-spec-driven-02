# Plano técnico

## Contexto
Implementa a especificação do encurtador de links, respeitando a constituição
do projeto.

## Arquitetura
- API HTTP em Minimal API, um arquivo de endpoints por recurso
- Camada de aplicação com LinkService, dona das regras RN-01 a RN-04
- Camada de dados com EF Core e um repositório por agregado
- Endpoint só valida entrada, chama o serviço e traduz o resultado em resposta

## Decisões
- D-01: SQLite como armazenamento, porque o RF-08 exige persistência
  entre reinícios
- D-02: contador incrementado por UPDATE atômico no banco, e não lido e
  reescrito na aplicação, para atender a RN-04 sob acesso concorrente
- D-03: código do link gerado com alfabeto sem caracteres ambíguos, para
  reduzir erro de digitação em link lido em voz alta
- D-04: unicidade do código garantida por índice único no banco, além da
  checagem no serviço, porque a checagem sozinha tem corrida
- D-05: chave de gerenciamento da DC-03 armazenada como hash, nunca em texto puro
- D-06 (T-10): cache de redirects singleton em memória, limitado a 1024 links,
  com descarte do menos recentemente acessado (LRU) e validade absoluta de
  cinco minutos. Guarda apenas ID, destino e expiração, sem entidades EF ou
  chaves de gerenciamento. A expiração é verificada em cada redirect e o
  contador continua usando UPDATE atômico; estatísticas consultam o banco.
  Usa coleções da biblioteca padrão, sem novas dependências.

## Modelo de dados
Link
- Id, Code (único), TargetUrl, CreatedAtUtc, ExpiresAtUtc (nulo), AccessCount,
  ManagementKeyHash

## Contratos
- POST /api/links entrada: targetUrl, code opcional, expiresAtUtc opcional
- POST /api/links saída: shortUrl, code, managementKey
- GET /{code} redireciona com 302, ou 404 se não existe, ou 410 se expirou
- GET /api/links/{code}/stats exige header com a chave de gerenciamento

## Riscos
- Concorrência na criação de código próprio, mitigada por D-04
- Crescimento da tabela de links sem política de limpeza, fora de escopo por ora
