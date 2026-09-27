# ChatApp — Segurança e confiança

Esta versão aplica uma camada de segurança para autenticação, APIs, SignalR, uploads, grupos, chamadas e reuniões.

## Proteções implementadas

- Password policy mais forte e lockout após tentativas falhadas.
- Cookie de autenticação HttpOnly + SameSite Strict.
- Rate limiting para endpoints `/api`.
- Cabeçalhos HTTP de segurança.
- Erros internos de base de dados não são devolvidos ao cliente no fluxo de criação de grupos.
- Uploads usam nomes aleatórios e validação por assinatura do conteúdo, não apenas pela extensão/MIME enviado pelo navegador.
- Limites de tamanho para uploads e mensagens.
- URLs de fotos de grupos e status ficam limitadas aos uploads internos esperados.
- Status só pode ser visualizado por amigos ou pelo próprio autor.
- Visualização de status é protegida contra duplicação concorrente.
- Hubs de grupos exigem autenticação e membership antes de entrar, enviar mensagens ou iniciar/entrar em chamadas.
- Sinalização WebRTC de grupo só é entregue entre participantes da mesma chamada.
- Chamadas individuais usam uma lista temporária de participantes autorizados no servidor.
- Reuniões só podem ser consultadas/acedidas por participantes.
- Preferências de notificações são respeitadas pelo serviço central de notificações.

## Produção

1. Definir `ConnectionStrings__DefaultConnection` como variável de ambiente/secret.
2. Definir `DataProtection__KeysPath` para um diretório persistente. Em múltiplas instâncias, todas devem partilhar as mesmas chaves.
3. Usar HTTPS obrigatório.
4. Não colocar passwords, connection strings ou chaves privadas no repositório.
5. Usar armazenamento de objetos (S3/Azure Blob/etc.) para uploads em produção quando a infraestrutura deixar de ser um único servidor.
6. Fazer backup periódico da base de dados.
7. Monitorizar logs de autenticação, erros e rate limiting sem guardar passwords, tokens ou conteúdo privado desnecessário.
8. Manter o .NET e os pacotes NuGet atualizados.

## Limitações atuais

A camada de segurança não substitui uma auditoria externa/penetration test antes de uma utilização comercial em escala. Também é recomendável migrar media privada para endpoints autorizados ou storage privado caso o produto venha a armazenar documentos sensíveis.
