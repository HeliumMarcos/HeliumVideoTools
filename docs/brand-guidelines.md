# Helium Vídeo Tools — identidade aprovada

Em 30/10/2026, o usuário aprovou a proposta A, **Vídeo em movimento**, com a assinatura Helium / Vídeo Tools.

## Fonte da marca

- `assets/logo.svg`: mestre vetorial ativo, promovido da proposta `video-flow.svg`.
- `assets/logo-512.png`: ícone transparente renderizado do mestre.
- `assets/logo.ico`: ícone Windows com resoluções 16/20/24/32/40/48/64/128/256.
- `assets/brand/logo-signature-dark.svg` e `logo-signature-light.svg`: assinatura completa vetorial com tipografia em caminhos.
- Assinaturas PNG em dobro da resolução base para uso WPF sem biblioteca SVG externa.

O mestre conserva o quadrado arredondado com borda #171B1E, gradiente azul #00A8F5 → #0063BC e pictograma branco. Moldura de vídeo, play e seta representam transformação e saída. A assinatura destaca Helium e usa Vídeo / etiqueta Tools na linha inferior.

Não adicionar órbitas, letras dentro do ícone, brilhos, sombras, gradientes novos ou detalhes extras. Não deformar os arquivos. Preservar transparência e margem interna. O padrão visual foi escolhido com base em DownloadRush e YTMusicRush fornecidos pelo usuário.

## Geração e integração

`scripts/generate_icons.py` gera os PNGs/ICO a partir dos mestres ativos. Não lê mais o antigo submark HV. O preparo dos arquivos históricos da marca continua separado e não deve substituir o mestre aprovado.

A prévia .NET usa a nova assinatura no cabeçalho, o ICO na janela e o mesmo ICO no executável. Nome do produto, caminho do executável e AppUserModelID permanecem inalterados para preservar a identidade dos atalhos.

A distribuição instalada ainda não foi substituída; os recursos atualizados serão incluídos no próximo pacote validado. Artes históricas e propostas continuam no projeto para referência.
