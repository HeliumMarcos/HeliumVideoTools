# Helium Vídeo Tools 3.0.1 — versão oficial

Windows x64, C#/.NET 10 com WPF, runtime incluído. Não requer Python nem instalação separada de .NET. Publicada no canal estável por aprovação do usuário.

Novidades da 3.0.1:

- Barra de título escura na janela principal e em Atualizações, mantendo os controles nativos do Windows 11. Em alto contraste, as cores da moldura voltam ao padrão do sistema.
- Janela de atualizações mais compacta, com FFmpeg/ffprobe em uma linha e detalhes completos no tooltip e na descrição de acessibilidade.
- Download com porcentagem real, cancelamento e atividade durante a verificação. O instalador fecha e reabre o app; não é exibida uma porcentagem fictícia de instalação.
- Sem rolagem no tamanho padrão nos estados de repouso e download; rolagem automática permanece como segurança para telas menores e mensagens extensas.
- Versão superior à 3.0.0 para permitir testar a notificação do canal oficial na instalação anterior.

- Visual e marca aprovados, ação principal fixa, processamento de vários arquivos sem fila visível.
- Divisão, compactação e conversão WebM, progresso geral, cancelamento e abertura da pasta de saída.
- Alvo padrão de 98 MB decimais; WebM usa um teto e não aumenta o orçamento de vídeos menores.
- FFmpeg e ffprobe 9.0.2 incluídos a partir do mesmo ZIP estável com SHA-256 verificado.
- Instalação por usuário, sem elevação, em `%LOCALAPPDATA%\Programs\HeliumVideoTools`; executável em `app\HeliumVideoTools.exe`.
- Reinstalação preserva atalhos existentes e encerra somente a instalação de destino. Mantém o último diretório do aplicativo como backup.
- Janela Atualizações aprovada e integrada: consulta pública de Releases, download com progresso/cancelamento e verificação SHA-256 fora da thread da interface, antes de chamar o instalador.
- Consulta automática de novas versões estáveis após a janela abrir. Pré-releases não entram no canal de atualização estável.
- Versões instaladas de FFmpeg/ffprobe lidas dos binários; atualização conjunta para conjunto imutável e opção de restaurar o anterior.
- Auxiliar de atualização sem janela de console.

Os vídeos originais são preservados. A instalação não altera as distribuições Python portáteis anteriores na pasta Downloads.

O canal oficial não representa certificação de aceitação no Projudi/SEEU nem validação completa de audiências reais. O teste automatizado de reinstalação não comprova todos os comportamentos de fixação do Windows. Ainda é necessário validar o ciclo completo de atualização no computador do usuário e a transação completa das ferramentas. Consulte `docs/MIGRACAO-ANDAMENTO.md` para limitações e pendências.

Não há certificado Authenticode configurado. `SHA256SUMS.txt` acompanha o instalador para verificar integridade.
