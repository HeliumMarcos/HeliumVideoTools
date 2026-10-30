# Migração .NET — primeira etapa

Estado em 30/10/2026: implementação C#/.NET 10 WPF incorporada à main; repositório público por decisão do usuário. Distribuição em validação no GitHub Actions; ainda não constitui release estável.

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

O usuário aprovou o visual V2 das três ferramentas e a identidade A (Vídeo em movimento), já aplicada e registrada em `docs/brand-guidelines.md`. Novas telas devem ser apresentadas por comparação antes/depois antes de ativar na versão final.

## Pendências antes de release

- Completar validação de DPI, tema claro/alto contraste e teclado; visual escuro e ícone já aprovados.
- Portar preferências com importação do perfil antigo e preservar padrões de abertura.
- Divisão por tamanho agora recodifica em H.264/AAC e ajusta a duração até respeitar o limite. Testes conferem cada tamanho e soma de durações, incluindo sobra final curta. Ainda falta validar continuidade perceptual com amostras extensas/quadros variáveis.
- Codificação acima do limite agora repete com orçamento reduzido, até três tentativas; falha final não publica arquivo excedente.
- Recuperar resultados parciais de um lote e relatar erros por arquivo; a prévia interrompe o lote no primeiro erro.
- Serviço de atualização pública sem credenciais implementado e testado para hash inválido, origem, versão igual e HTTP 403; falta ativar interação no app após apresentar a nova tela ao usuário.
- Serviço de ferramentas em conjuntos imutáveis, validação, troca atômica de ponteiro e restauração implementado; falta validar transação completa e integrar interação.
- Instalador NSIS por usuário e workflow GitHub Actions criados. Validar upgrade real, pasta fixa e atalhos; fixação real continua dependente de validação no Windows do usuário.
- Publicar o instalador em Releases somente após esses requisitos.

## Compilar/testar

Com SDK .NET 10:

```powershell
dotnet build src/HeliumVideoTools.Desktop -c Release
dotnet run --project tests/HeliumVideoTools.Tests -c Release
.\scripts\Prepare-MediaTools.ps1
```

O SDK local de desenvolvimento foi extraído em `work/tools/dotnet` após conferir SHA-512 contra os metadados oficiais da Microsoft. `work` e binários não são enviados ao Git.

O manifesto fixado em `tools/manifest.json` prepara o conjunto do instalador. Ferramentas posteriormente atualizadas ficam no perfil fora da instalação, para não serem rebaixadas pelo instalador. O cliente de Releases usa acesso público anônimo, sem dependência de GitHub CLI ou tokens.
