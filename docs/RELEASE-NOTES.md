# Helium Vídeo Tools — segunda prévia .NET

Prévia técnica Windows x64, C#/.NET 10 com WPF, runtime incluído. Não requer Python nem instalação separada de .NET.

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

Esta prévia ainda não representa validação completa de uma audiência real ou aceitação no Projudi/SEEU. O teste automatizado de reinstalação não comprova todos os comportamentos de fixação do Windows. A interface de atualizações está integrada, mas falta validar seu ciclo ponta a ponta entre duas versões estáveis publicadas e a transação completa de ferramentas. Consulte `docs/MIGRACAO-ANDAMENTO.md` para as limitações e pendências.

Não há certificado Authenticode configurado. `SHA256SUMS.txt` acompanha o instalador para verificar integridade.
