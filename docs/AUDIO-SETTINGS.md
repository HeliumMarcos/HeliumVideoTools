# Ajustes de áudio na compactação — 3.0.16

Prévia aprovada pelo usuário em 03/11/2026. Em Compactar, desligar **Áudio original** revela **Taxa de áudio** e **Canais** lado a lado. O codec de recodificação é AAC.

- Taxas disponíveis: 64, 96, 128, 160, 192, 256 e 320 kbps. Padrão: 128 kbps.
- Canais: manter os canais do arquivo, mono ou estéreo. Padrão: manter canais. Esse modo não envia `-ac`; conversão explícita usa 1 ou 2 canais.
- Ao ligar Áudio original, os campos desaparecem e a cópia ignora os ajustes ocultos. Ao desligar novamente, preserva as escolhas enquanto o editor atual existir. Ao voltar ao início/reabrir, mantém o padrão protegido de áudio original ligado e campos 128 kbps/manter canais.
- Desligar a opção não remove o som. Fontes sem áudio continuam sem áudio.
- O orçamento de vídeo desconta a taxa AAC selecionada, tanto no motor quanto na estimativa. Com um teto de tamanho, reduzir a taxa de áudio disponibiliza mais orçamento ao vídeo; não obriga a saída a ficar proporcionalmente menor. O tamanho real continua conferido e o vídeo é ajustado se necessário.
- Ajustes são capturados no início do lote e bloqueados durante processamento. Mudar parâmetros após um resultado retorna à edição para permitir novo processamento.
- WebM permanece com Opus a 128 kbps; a divisão conserva sua configuração anterior. Os novos controles se aplicam à compactação MP4.
- Em áreas mais baixas, o painel usa rótulos laterais e oculta explicações redundantes; campos, valores e estimativa de teto permanecem acessíveis. O tooltip identifica AAC. Não se corta conteúdo em janelas extremas: mantém o fallback de acesso por rolagem já existente.

## Verificações

`--verify-audio-encoding work/audio-encoding-validation`: gera somente mídia sintética de oito segundos, 640×360/25 fps, estéreo. Com FFmpeg/ffprobe reais, confere AAC a 64/192/128 kbps (tolerância ao bitrate médio efetivo do encoder), mono/estéreo/canais originais, uma/duas etapas, teto em bytes, decodificação e SHA-256 da fonte intacto. A cópia original é conferida por hash dos pacotes AAC. Fonte sem som permanece sem trilha de áudio. Também valida orçamento impossível, taxas/canais inválidos, opções ocultas ignoradas e compatibilidade do JSON antigo com os novos campos opcionais.

`--verify-audio-settings work/audio-settings-validation`: confere mostrar/ocultar, padrões, mapeamento taxa/canais, preservação das escolhas ao alternar, retorno à edição depois do resultado, reset e redimensionamento em início/progresso/resultado. Testa áreas lógicas 1120×700, 1280×650, 1536×800, 900×650 e 675×449 com estimativa completa. Nas quatro primeiras, exige ausência de rolagem geral e campos dentro da janela; na última, preserva acesso e valores. Simulação lógica não equivale a teste físico de todos os monitores/escalas.

As duas verificações integram o workflow Windows, junto com os testes existentes de processamento, temas, teclado, fechamento, layout e recuperação. Arquivos de ensaio e imagens ficam em work. Preferências e vídeos do usuário não são usados nos ensaios.
