# Helium Vídeo Tools 3.0.2 — lotes e cortes precisos

- Um vídeo com erro não interrompe os demais arquivos selecionados. O resultado distingue concluídos, falhas, cancelados e não iniciados.
- Partes já concluídas são preservadas quando o restante falha ou é cancelado. Relatório TXT/JSON local em `%LOCALAPPDATA%\HeliumVideoTools\reports`.
- Resumo final aprovado, sem fila de tarefas: Ver relatório, Abrir pasta ou escolher entre várias pastas de saída.
- Progresso geral não regride quando há nova tentativa de codificação e ignora notificações atrasadas de arquivos encerrados.
- Correção de sobreposição de quadros nos cortes por quantidade/duração. Divisões agora geram MP4 H.264/AAC com busca precisa e recodificação, também preservando a cadência variável. Isso pode ser mais demorado que copiar fluxos sem recodificar.
- Testes sintéticos CFR/VFR verificam sequência e quantidade de quadros, áudio decodificável e durações A/V; lotes reais incluem arquivo corrompido, cancelamento após a primeira parte e múltiplas pastas. Não substituem validação perceptual de audiências extensas ou aceitação no Projudi/SEEU.

Continuam: alvo inicial 98 MB, WebM limitado pelo teto e pelo tamanho original, FFmpeg/ffprobe 9.0.2 incluídos, instalação por usuário sem administrador na mesma pasta, atualização pelo app com SHA-256 e reabertura automática.

Os originais não são alterados. Relatórios permanecem apenas no computador, sem envio ao GitHub. Não há certificado Authenticode configurado; `SHA256SUMS.txt` acompanha o instalador.
