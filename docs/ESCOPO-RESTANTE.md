# Escopo restante aprovado — 01/11/2026

Após a publicação da 3.0.5, o usuário limitou a continuação aos itens 2, 3, 5 e 6 da lista apresentada. Este documento define o escopo ativo; listas históricas não ampliam esse escopo.

## Situação atual após a entrega conjunta 2 + 3 — 3.0.14

**Decisão posterior do usuário (02/11/2026):** retirar do escopo a escuta com Narrador e os testes físicos de escalas/monitores. Não são mais pendências a executar nem devem ser solicitados novamente. Permanecem registrados como não realizados, sem transformar testes automatizados em validação física/auditiva. A entrega acordada dos itens 2 e 3 fica encerrada, com os limites técnicos documentados.

**Continuação do item 5:** instalação real 3.0.14 medida, incluindo seis aberturas, memória simultânea do app/filhos e processamento pela interface com fonte sintética de 180 s/720p. Abertura repetida mediana 727 ms; H.264 18,28 s, divisão 12,78 s e VP8 59,45 s. Saídas/áudio/hash/preferências conferidos. Sem alterações no app ou nova release. Referência e limitações em [DESEMPENHO-INSTALADO.md](DESEMPENHO-INSTALADO.md). Resta a observação de abertura fria controlada; não houve reinício/cache limpo nesta sessão. O comando só de abertura está preparado para observação futura após reinício decidido pelo usuário.

- **Item 2, implementação:** histórico de lote interrompido implementado com snapshot atômico/lease, recuperação em relatórios idempotentes e restauração do resumo existente; saídas concluídas preservadas, ausentes/alteradas identificadas, sem retomada automática. Soma-se às recuperações de temporários, ferramentas e instalador já entregues. Testes de morte real do worker e distribuição passaram em 41493104918.
- **Item 3, implementação e testes automatizados:** layout/áreas menores, nomes e eventos acessíveis, teclado, alto contraste e renderização do resumo em 100/125/150/200% verificados. Escuta do Narrador deixada pendente por escolha expressa do usuário neste turno; testes físicos em outras escalas/DPI misto permanecem dependentes de ambiente adequado. Não declarar esses testes físicos concluídos.
- **Item 5:** instalação, memória da árvore FFmpeg e mídia sintética maior medidas. Abertura fria controlada continua não medida. A correção do host/medições históricas continua válida.
- **Item 6:** extrações planejadas implementadas e testadas.

[BATCH-RECOVERY.md](BATCH-RECOVERY.md) registra evidências e limites (inclusive janela publicação/registro e falha física de energia não simulada). A [3.0.14 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.14) teve download/atualização anônima conferidos em 41493942250. O histórico abaixo preserva a evolução; referências a tarefas futuras em entregas anteriores não substituem este estado atual.

Distribuição atualizada em 02/11/2026: o usuário dispensou novas cópias de instaladores na pasta Music. Continuar gerando e publicando os instaladores oficiais pelo GitHub, sem nova cópia local nessa pasta.

## 2. Recuperação após interrupções

- Processamento: registrar temporários próprios e resultados publicados; no próximo início, recuperar o estado de uma operação interrompida sem apagar originais ou saídas concluídas.
- Encerramento abrupto: verificar o destino dos processos FFmpeg iniciados pelo app e evitar processos órfãos.
- Ferramentas: testar interrupções reais de download, extração e ativação; manter o conjunto anterior ou o embutido utilizável.
- Instalador: testar interrupções durante a substituição e recuperação da instalação anterior, preservando perfil e vídeos.
- Usar processos e diretórios de teste isolados. Não simular uma queda de energia desligando o computador do usuário.

Auditoria inicial: `VideoEngine.RunOutput` e a pasta de estatísticas de duas etapas usam `finally` para remover temporários. Isso cobre cancelamento/erros normais, mas não executa após encerramento forçado ou queda de energia. A próxima implementação deve registrar a propriedade desses temporários antes de criá-los, para permitir limpeza específica no próximo início; não procurar e apagar arquivos pela extensão nas pastas dos vídeos.

Etapa 3.0.6: registro durável/exclusivo de temporários de vídeo e estatísticas, limpeza em segundo plano ao abrir e Job Objects para os processos próprios implementados. Teste mata apenas o hospedeiro enquanto FFmpeg grava um MP4; confirma encerramento do filho, preservação de FFmpeg independente, saídas e arquivos desconhecidos. A etapa não retoma codificação nem conclui o item 2 inteiro: interrupções reais do atualizador de ferramentas e do instalador, recuperação de histórico do lote e queda de energia permanecem pendentes. Referência do mecanismo de processos: https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects.

Continuação 3.0.7 do item 2: transação das ferramentas registrada por ID e protegida por bloqueio; recuperação na abertura valida ativo/anterior, preserva conjuntos publicados e limpa somente staging conhecido. Oito casos encerram processos durante download, extração, gravação do backup, ativação e corrupção posterior. Instalador passa a restaurar a pasta anterior ao ser executado novamente após a troca interrompida; o workflow testa extração, intervalo de renomeação e conteúdo residual em raízes isoladas, com payload controlado. Queda física de energia e reconstrução de histórico não são declaradas validadas por esses testes.

Publicada e validada na execução 41379179432: os testes de interrupção do instalador passaram junto com os de ferramentas, processamento e interface. Próxima etapa planejada: medições de desempenho de referência (item 5), antes da reorganização interna (item 6); o item 2 ainda tem as limitações descritas acima.

## 3. Telas e acessibilidade

- Validar 100%, 125%, 150% e 200%, área útil pequena e movimentação entre monitores com escalas diferentes.
- Corrigir posicionamento, dimensionamento e rolagem quando a área útil for menor que o mínimo atual de 980×700 DIPs.
- Verificar Tab/Shift+Tab, seleção e cancelamento por teclado, leitor de tela, nomes/estados dos campos e anúncios de progresso sem excesso.
- Ampliar testes de cores de alto contraste e estados habilitado/desabilitado/foco.
- Preservar os temas e o layout aprovados. Apresentar antes/depois e obter aprovação antes de integrar qualquer mudança visual.

## 5. Desempenho

Continuação do item 3 na [3.0.13 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.13): LiveRegionChanged com controle de frequência, cliente UIA separado confirmando eventos/textos, navegação WPF Tab/Shift+Tab e teste no monitor disponível (144 DPI/150%). Suíte/distribuição em 41487771927 e download/atualização pública em 41488625824 passaram. Não houve mudança visual. Restam Narrador com áudio e campanha física de escalas/monitores mistos; não existe par de monitores com DPI diferente neste ambiente. Detalhes em ACCESSIBLE-PROGRESS.md.

Correção do item 5 nesta etapa: os hosts WPF antigos podiam abrir uma segunda janela via StartupUri. A garantia de isolamento das antigas medições de abertura/memória foi retirada; preservá-las como histórico e usar a nova série com uma janela confirmada, sem comparar runtimes/DPI diferentes como ganho/regressão. O host de testes foi isolado, a abertura produtiva ficou explícita e passou na instalação. DESEMPENHO.md registra método/limites. Abertura fria/instalada e memória da árvore completa continuam pendentes.

Etapa aprovada/publicada do item 3: [3.0.12](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.12), com adaptação a áreas menores, reflow reversível, limites nativos de monitor, manifesto PerMonitorV2 e nomes acessíveis dos botões de arquivos/progresso. Suíte de 30 áreas/operações antes/depois, 36 combinações de estados/tamanhos e ajuste de janela real passou em 41466735914, junto com distribuição; atualização/download anônimo confirmado em 41468872516. Tamanho padrão preservado e dimensões compactas lembradas nas preferências. Simulação não substitui DPI físico, múltiplos monitores ou Narrador; esses continuam pendentes. Detalhes em RESPONSIVE-ACCESSIBILITY.md.

- Registrar uma base reproduzível de abertura fria/quente, tempo até a janela utilizável e memória.
- Medir compactação, divisão e WebM com mídia controlada maior e parâmetros fixos, registrando versão do FFmpeg, hardware e configuração.
- Comparar antes/depois das otimizações e da reorganização interna. Não atribuir à linguagem ganhos determinados pelos codecs.
- Corrigir gargalos comprovados; evitar repetir grandes testes sem uma hipótese ou mudança a verificar.

Referência inicial do item 5 registrada em [DESEMPENHO.md](DESEMPENHO.md) e JSON associado: seis aberturas isoladas da janela real, memória do host no ponto ocioso e duas execuções por operação com mídia sintética de 45 s/720p. Ainda não mede abertura fria controlada, instalação self-contained ou memória completa com FFmpeg. Não houve otimização de produção; a referência permite começar o item 6 e comparar o mesmo cenário depois.

## 6. Organização interna

Em 02/11/2026, o benchmark foi repetido a pedido do usuário, que relatou programas pesados na execução inicial. Mesmos hashes de entrada/ferramentas e parâmetros; médias de 4,89 s (H.264), 3,41 s (divisão) e 14,33 s (VP8), abertura repetida mediana 796 ms e memória média do host 133,7 MB. Usar a repetição como referência preferencial, mantendo a medição inicial. Detalhes e limitações em DESEMPENHO.md.

- Separar a infraestrutura de processos, sondagem, ferramentas, atualizações, preferências e logs da lógica de domínio, conforme a direção do plano original.
- Reduzir responsabilidades concentradas no código da janela, mantendo o comportamento aprovado.
- Definir interfaces nos pontos em que isolamento e testes precisam delas, sem abstrações que apenas dupliquem o código.
- Manter compilação, testes de mídia, recuperação, temas, fechamento e distribuição passando após cada etapa.

## Sequência de execução

Segunda entrega do item 6 (3.0.9): OperationEditor assume campos/leitura/preferências compatíveis das operações e BatchResultPresentation assume textos/contagens/estados do resultado. Janela principal reduzida de 334 para 235 linhas não vazias; configuração e apresentação testadas antes/depois, sete imagens de tema idênticas por SHA-256. Testes de mídia/recuperação/fechamento e benchmark preservam as verificações anteriores. Atualização/encerramento/coordenação de estimativas ainda estão na janela; não declarar todo o item 6 concluído. Detalhes em REFACTOR-WINDOW.md.

Primeira entrega do item 6 (3.0.8): núcleo separado dos serviços externos em Infrastructure, execução/sondagem extraídas do motor, teste de fronteira e suíte funcional passando localmente. Benchmark comparativo registrado em DESEMPENHO.md, sem alteração material observada nesta amostra. Código da janela ainda precisa de uma etapa própria de redução de responsabilidades; a separação não declara o item 6 inteiro concluído.

1. Recuperação após interrupções (2).
2. Medições de desempenho de referência (5).
3. Organização interna (6), comparando com a referência de desempenho.
4. Validações/correções de telas e acessibilidade (3), com aprovação prévia das mudanças visuais.
5. Publicar versões oficiais após os testes de cada entrega; o instalador continua sendo produzido pelo GitHub Actions.

A segunda entrega foi publicada como [3.0.9 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.9), após sucesso da execução 41385183429 e verificação do download público e da detecção desde a 3.0.8. Próxima etapa: reduzir a coordenação ainda concentrada na janela; depois, validar telas e acessibilidade. Isso não encerra todo o escopo dos itens 2, 3, 5 e 6.

Terceira entrega do item 6: [3.0.10 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.10), validada pela execução 41389325642. Coordenação das estimativas extraída, testes de respostas atrasadas/cancelamento e consulta WPF real integrados ao GitHub. Sete imagens antes/depois idênticas. Download público, hash e atualização desde 3.0.9 conferidos. Próximo passo do item 6: coordenação de atualizações e encerramento; depois, telas/acessibilidade. Limitações dos demais itens continuam registradas acima.

Quarta entrega do item 6: [3.0.11 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.11), distribuição validada em 41390394652. Consulta inicial, janela Atualizações e encerramento extraídos; chamadas repetidas, espera/gravação, cancelamento e reabertura imediata testados. Sete imagens antes/depois idênticas com o mesmo teste estabilizado. Download anônimo e detecção desde 3.0.10 confirmados em 41390951856; a tentativa local foi limitada pela cota pública do GitHub. As extrações previstas da janela estão implementadas; próxima frente é o item 3 (telas/DPI/acessibilidade), com aprovação prévia de alterações visuais. As limitações dos itens 2 e 5 continuam explícitas, sem declarar todo o plano concluído.

## Fora do escopo desta continuação

- Item 1: campanha de validação com audiências longas reais e aceitação no Projudi/SEEU.
- Item 4: validação manual de atalhos fixados no Iniciar/barra de tarefas.
- Item 7: certificado e assinatura digital Authenticode.

Essas validações não são declaradas concluídas. A preservação dos arquivos de atalho já testada e as proteções existentes de tamanho/saída continuam no produto.
