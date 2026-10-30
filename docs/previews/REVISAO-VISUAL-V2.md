# Proposta visual V2 — aguardando aprovação

30/10/2026. Apenas rascunho na branch de migração; não substitui a distribuição instalada.

## Alterações propostas

- Ação principal fora do ScrollViewer, sempre visível no rodapé, com ícone e texto Compactar/Dividir/Converter.
- Sidebar com ícones vetoriais locais e seleção indicada por fundo, texto e borda de acento.
- Cabeçalho com destino Projudi/SEEU; marca atual preservada enquanto o novo ícone aguarda proposta própria.
- Cards compactos, ícones nas opções, estimativa destacada e interruptores de áudio/precisão.
- Divisão mostra só os campos pertinentes ao modo escolhido. Presets não personalizados ocultam tamanho personalizado.
- Foco visível em botões, campos e interruptores; nomes acessíveis nos controles numéricos/unidade.
- Campos longos com rolagem vertical, sem empurrar a ação principal para fora da janela.

## Referências e escolhas

Skill UI/UX Pro Max aplicada: consulta WPF validou Grid/ScrollViewer e navegação/foco de teclado. Consultas gerais de design retornaram também padrões de sites que não correspondem a esta ferramenta desktop; esses padrões e suas paletas não foram adotados. Mantidos fundo escuro, Segoe UI e azul Fluent da interface original. Sem fontes, ícones ou recursos carregados da rede.

## Verificação

Build Release: zero avisos e erros. As três telas foram renderizadas a 1280×900 e 980×700 no DPI do Windows deste computador (150%). Teste de geometria verifica que a ação principal está integralmente dentro da janela e não muda de posição ao rolar. Estados com painel de progresso também passam nessa checagem.

Capturas são renderizações da janela WPF real com formulário vazio, não maquetes geradas. Ainda não representam validação interativa completa, temas claro/alto contraste ou todos os DPIs.

## Comparação

- `compactar-python-original.png`: aplicação Python de referência.
- `compactar-wpf-anterior.png`: primeira proposta WPF que não exibia a ação principal.
- `compress-wpf-v2.png`: nova proposta Compactar.
- `split-wpf-v2.png`: nova proposta Dividir.
- `webm-wpf-v2.png`: nova proposta Converter.
- Sufixo `980x700`: janela mínima com formulário rolável e ação fixa.

Próximas alterações visuais e novo ícone devem continuar sendo apresentados antes de adotar na versão final, conforme solicitado pelo usuário.
