# Escopo restante aprovado — 01/11/2026

Após a publicação da 3.0.5, o usuário limitou a continuação aos itens 2, 3, 5 e 6 da lista apresentada. Este documento define o escopo ativo; listas históricas não ampliam esse escopo.

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

## Fora do escopo desta continuação

- Item 1: campanha de validação com audiências longas reais e aceitação no Projudi/SEEU.
- Item 4: validação manual de atalhos fixados no Iniciar/barra de tarefas.
- Item 7: certificado e assinatura digital Authenticode.

Essas validações não são declaradas concluídas. A preservação dos arquivos de atalho já testada e as proteções existentes de tamanho/saída continuam no produto.
