# Helium Vídeo Tools 3.0.3 — estimativas e preferências

- Apresentação das estimativas aprovada: separa orçamento dos codecs, teto aplicado e referência de tamanho baseada no original.
- Compactação e WebM usam as mesmas regras de orçamento do motor. Um original de 40 MB não recebe um orçamento de 299 MB na conversão.
- Divisão por quantidade/duração mostra referência por parte e duração, sem somar todos os originais como se fossem uma única parte. A referência não promete o tamanho após recodificar. Divisão por tamanho mostra o teto, sem inventar uma quantidade final de partes.
- Metadados analisados em segundo plano, com cache por tamanho/data, até duas sondagens paralelas e cancelamento das solicitações antigas. Detalhes por arquivo no tooltip.
- Preferências úteis persistem fora da instalação: última pasta escolhida, dimensões/maximização, partes/minutos da divisão e modo em duas etapas. Importação inicial de opções compatíveis da versão Python, sem modificar o registro legado.
- Mantidos os padrões exigidos: abrir em Compactar, Personalizada/98 MB, áudio original ligado e conversão VP8. Tamanho-alvo antigo, codec, preset e página inicial não são restaurados.
- Configuração inválida é preservada em backup e não impede iniciar com os padrões.

O valor exibido é orçamento ou referência, não promessa de tamanho exato. O motor continua conferindo o tamanho real antes de publicar cada saída. Relatórios, preferências e vídeos ficam locais; não são enviados ao GitHub.

Instalação por usuário, sem administrador, na mesma pasta; .NET e FFmpeg/ffprobe incluídos. `SHA256SUMS.txt` acompanha o instalador; não há assinatura Authenticode configurada. Validações com audiências extensas, temas/acessibilidade e transação completa de ferramentas permanecem no plano.
