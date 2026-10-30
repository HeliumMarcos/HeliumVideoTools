# Migração .NET — primeira etapa

Estado em 30/10/2026: prévia técnica, ainda não substitui o aplicativo Python e não constitui release estável.

Implementado:

- Projetos C#/.NET 10 para motor e interface WPF, sem dependências NuGet externas.
- Sondagem ffprobe JSON; execução assíncrona de FFmpeg, cancelamento da árvore do processo e logs rotativos locais.
- Compactação H.264/H.265, WebM VP8/VP9 e divisão; modo em duas etapas para codificação.
- MB decimais, orçamento com margem e validação do tamanho real antes de publicar a saída.
- WebM usa o menor orçamento entre o limite e 98% do tamanho original. Um MP4 menor não é expandido para ocupar 299 MB.
- Arquivos temporários próprios, publicação sem sobrescrever, originais preservados.
- Interface inicial em rascunho: seleção múltipla, arrastar/soltar, operações diretas sem fila, progresso geral, cancelamento e Abrir pasta.
- Conjunto do projeto atualizado de FFmpeg/ffprobe 8.1.2 para 9.0.2, mesma distribuição Gyan Essentials, ZIP conferido por SHA-256 e codecs necessários verificados. Script de preparação guarda backup e tenta restaurar o conjunto se a cópia falhar.

Validação: compilação Release sem avisos/erros; testes com mídia artificial incluindo áudio, compactação em duas etapas, VP8/VP9, limite sem aumento sobre original, divisão por quantidade, progresso, cancelamento e limpeza de temporários. Isso não atesta aceitação no Projudi/SEEU nem valida ainda a continuidade audiovisual de uma audiência dividida.

## Aprovação visual

O usuário pediu comparação antes de alterações visuais. O rascunho WPF já existia quando a orientação foi recebida. As imagens em `docs/previews` servem para revisar essa proposta; a versão Python permanece a referência instalada. Próximas mudanças de layout e ícone aguardam aprovação por comparação antes/depois. O novo ícone ainda não foi produzido.

## Pendências antes de release

- Aprovar/refinar visual das três telas, DPI, tema claro/alto contraste, teclado e ícones.
- Portar preferências com importação do perfil antigo e preservar padrões de abertura.
- Garantir divisão por tamanho com repetição adaptativa e validar ausência de lacunas. Atualmente, se um trecho copiado exceder o limite, ele é rejeitado com mensagem; não há ajuste automático da duração ainda.
- Refinar repetição de codificação acima do limite: atualmente a saída excedente é rejeitada, sem publicação. Não anunciar sucesso nesse caso.
- Recuperar resultados parciais de um lote e relatar erros por arquivo; a prévia interrompe o lote no primeiro erro.
- Adaptar atualização autenticada de releases privadas e ferramentas no próprio app, com rollback transacional e testes de falha.
- Criar instalador sem elevação e workflow GitHub Actions; validar upgrade real, pasta fixa e atalhos fixados.
- Publicar o instalador em Releases somente após esses requisitos.

## Compilar/testar

Com SDK .NET 10:

```powershell
dotnet build src/HeliumVideoTools.Desktop -c Release
dotnet run --project tests/HeliumVideoTools.Tests -c Release
.\scripts\Prepare-MediaTools.ps1
```

O SDK local de desenvolvimento foi extraído em `work/tools/dotnet` após conferir SHA-512 contra os metadados oficiais da Microsoft. `work` e binários não são enviados ao Git.

O manifesto fixado em `tools/manifest.json` permite preparar as ferramentas do build. Não se deve incluir automaticamente o FFmpeg antigo num pacote novo. O script atual é preparação de desenvolvimento/CI; a atualização transacional dentro do app permanece pendente.
