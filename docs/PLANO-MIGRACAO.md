# Helium Vídeo Tools — migração Windows

Atualizado em 30/10/2026. Este documento registra a direção e os critérios originais. A migração C#/.NET 10 WPF, identidade visual, instalador e release oficial 3.0.1 já foram entregues; o usuário confirmou a atualização pelo app. A etapa 3.0.2 integra lotes tolerantes a falhas e cortes precisos. Consulte `MIGRACAO-ANDAMENTO.md` para evidências e pendências reais; não interpretar etapas históricas abaixo como itens ainda não iniciados.

## Decisão técnica

Migrar para C# / .NET 10 LTS com WPF, Windows x64. C# é a linguagem; .NET é a plataforma, portanto não são duas alternativas concorrentes.

| Opção | Adequação ao projeto | Decisão |
| --- | --- | --- |
| C++ com Qt ou WinUI | Bom controle nativo; maior custo de implementação, distribuição e manutenção. O processamento pesado continuaria no FFmpeg externo. | Não escolhido |
| C# / .NET com WPF | XAML permite reproduzir o layout, integração Windows e processos assíncronos; já existe experiência no DownloadRush. | Escolhido |
| C# / .NET com Avalonia | Interface Fluent e experiência existente no YTMusicRush; útil para multiplataforma. | Referência de atualização, sem necessidade multiplataforma neste produto |

Publicação self-contained em pasta, com runtime incluído e sem extração onefile na inicialização. ReadyToRun será avaliado por medição. Não prometer ganho de velocidade de codificação devido à troca da linguagem: codecs, parâmetros, hardware e FFmpeg determinam a maior parte desse tempo. Medir abertura fria/quente, memória e operações equivalentes antes/depois.

## Referências verificadas

- YTMusicRush: `docs/UPDATES.md`, `src/YTMusicRush.Core/ReleaseUpdates.cs`, `installer/YTMusicRush.nsi` e `assets/icon.svg`, consultados em 30/10/2026. Usa Avalonia, releases privadas autenticadas, hash e instalador por usuário.
- DownloadRush: WPF/.NET 10; README registra preservação de atalhos, instalação por usuário e atualizador próprio. O próprio projeto registra que a fixação real no Windows ainda exige validação interativa.
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://learn.microsoft.com/en-us/windows/win32/shell/appids
- https://docs.github.com/en/rest/releases/assets
- https://ffmpeg.org/download.html

## Layout e comportamento preservados

Sidebar com marca e Dividir / Compactar / Converter; Compactar ao abrir. Cards, espaçamentos, tema claro/escuro e área arrastar/soltar próximos da versão original. Nada de fila visível: seleção múltipla inicia um lote direto. Progresso geral integrado, arquivo atual, contagem, ETA e cancelamento. Ao concluir com resultados, ação muda de Cancelar tudo para Abrir pasta. Para pastas diferentes, oferecer lista das pastas de saída.

Compactar inicia em Personalizada, 98 MB a cada abertura; número e unidade KB/MB/GB separados; manter áudio original ligado. Dividir por quantidade permite 2–10; preservar duração/tamanho. Converter começa em VP8 com Opus, com VP9 opcional. Estimativas nas três telas, explicitamente aproximadas. Validar tamanho-alvo, metadados, espaço e permissões; não sobrescrever vídeos existentes. Evitar herdar uma preferência antiga que desative esses padrões.

Adicionar Configurações / Atualizações sem alterar a organização das três ferramentas. Layout com Grid/ScrollViewer, altura mínima de controles, texto longo, foco visível, teclado e contraste; validar 100/125/150/200% de DPI e janela pequena. Comparar capturas das três telas, estados vazios, arquivos e progresso.

## Estrutura proposta

- `src/HeliumVideoTools.Core`: opções tipadas, modelos, orçamento de tamanho e comandos FFmpeg.
- `src/HeliumVideoTools.Infrastructure`: execução de processos, ffprobe JSON, preferências, logs, GitHub e ferramentas.
- `src/HeliumVideoTools.Desktop`: WPF/XAML, estilos e ViewModels.
- `src/HeliumVideoTools.UpdateHost`: coordenação de encerramento/atualização quando necessário.
- `tests/`: testes de domínio, processo, atualização e distribuição.
- `installer/` e `.github/workflows/`: instalador por usuário e build de releases.

Processos com ArgumentList, UseShellExecute=false e CreateNoWindow=true; ler stdout/stderr assincronamente, CancellationToken, timeout de sondagem e encerramento da árvore de FFmpeg. Progresso por `-progress pipe:1`. Arquivos de saída temporários e publicação após sucesso; cancelamento limpa somente temporários do próprio processamento. Logs rotativos locais e mensagens humanas. Preferências gravadas atomicamente em perfil do usuário, fora da pasta de instalação. Migrar as preferências úteis do QSettings somente uma vez.

## Instalação sem administrador e preservação de atalhos

NSIS por usuário, seguindo o fluxo do YTMusicRush: RequestExecutionLevel user, registro HKCU e atalhos do usuário atual. Primeira instalação em `%LOCALAPPDATA%\Programs\HeliumVideoTools`. Registrar InstallLocation e reutilizar a mesma pasta em todo upgrade; não criar subpastas por versão nem solicitar outro destino ao atualizar. Runtime acompanha o aplicativo.

Identidade estável: produto Helium Vídeo Tools, executável `HeliumVideoTools.exe`, AppUserModelID `Helium.HeliumVideoTools`, chave de desinstalação e caminho do `.lnk` fixos. Não executar desinstalação completa antes de um upgrade. Criar atalhos apenas quando ausentes; preservar atalhos existentes, destino, argumentos, identidade e ícone com caminho fixo. Corrigir identidade legada de forma pontual quando necessário, sem apagar/recriar todos os atalhos. Não manipular o banco de pins do Windows.

O instalador Python atual usa Program Files e não declara identidade explícita de atalho; isso é um risco identificado, não prova da causa do desaparecimento observado nos outros aplicativos. Uma instalação antiga protegida em Program Files pode exigir uma migração pontual autorizada pelo Windows; a nova instalação por usuário não deve tentar escrever nessa pasta ou prometer removê-la sem elevação.

Teste obrigatório: instalar versão A como usuário normal, fixar manualmente no Iniciar/barra de tarefas, atualizar para B com app aberto, conferir mesmos caminhos/identidade, pins preservados e inicialização correta. Testar atualização repetida e reinstalação. Não declarar esse requisito validado apenas por teste automatizado de atalhos.

## Atualização pelo aplicativo e Releases públicas

Repositório `HeliumMarcos/HeliumVideoTools`, tornado público a pedido do usuário em 30/10/2026. Consultar latest estável após a janela abrir e manualmente nas configurações; ignorar drafts/prereleases e versões iguais/anteriores. Mostrar versão instalada, disponível e notas. Aviso discreto com Baixar e atualizar / Depois. Download dentro do app, porcentagem e cancelamento; nenhuma necessidade de navegador para baixar.

Decisão atual: acesso público anônimo, sem sessão GitHub CLI, login, token ou armazenamento de credenciais. A hipótese anterior de autenticação para repositório privado foi substituída pela autorização explícita do usuário. Falha HTTP ou limite da API não podem ser anunciados como versão já atualizada.

Validar origem, tamanho, SHA-256 da API e SHA256SUMS antes de executar; repetir a verificação imediatamente antes da instalação. HTTPS e hash não equivalem a assinatura de editor: Authenticode permanece uma melhoria dependente de certificado. Redirecionamentos não recebem credencial fora do domínio da API GitHub.

Ao escolher instalar: coordenar encerramento, cancelar FFmpeg com limpeza e salvar preferências. Se houver vídeo em processamento, informar que a instalação interrompe a operação e permitir adiar até terminar. O instalador fecha a instalação alvo mesmo se aberto diretamente, tenta fechamento normal e força somente após prazo e identificação por caminho completo do executável. Bloquear execução simultânea de instaladores e novas instâncias durante a substituição; falhar sem sobrescrever arquivos se não puder encerrar. Não matar todo processo com nome semelhante nem FFmpeg de outros aplicativos. Reabrir ao concluir. Preservar perfil, ferramentas atualizadas e vídeos. Recuperar instalação anterior se a substituição falhar.

O app fica utilizável durante o download, mas precisa encerrar brevemente durante a substituição dos executáveis. Essa transição será automática.

## FFmpeg e ffprobe

Exibir versão instalada individual, caminho ativo e versão estável disponível; ações Verificar, Atualizar conjunto e Restaurar conjunto anterior. Sondar ambos com `-version` fora da thread da UI. Usar ffprobe JSON para metadados, em vez de interpretar a saída textual do FFmpeg atual.

Distribuir e atualizar os dois do mesmo pacote estável Windows (Gyan, referenciado pelo FFmpeg), com versão e hashes fixados em manifesto. Não usar simplesmente builds git/noturnos como versão mais recente. Atualizar em conjunto, só quando nenhum processo estiver usando as ferramentas. Baixar em staging, conferir hash, extrair apenas executáveis esperados com proteção de caminho, executar teste de versão e codecs, trocar com backup e rollback de ambos. Recuperar transação interrompida no próximo início.

Ferramentas versionadas no perfil local e seleção ativa persistida; pacote inicial no instalador como fallback. Upgrade do aplicativo não deve rebaixar nem apagar o conjunto já atualizado. Incluir licenças e informações de origem/redistribuição correspondentes à build escolhida.

## Ícone no padrão dos outros aplicativos

Referência observada: YTMusicRush usa quadrado escuro arredondado externo, placa interna com gradiente azul e pictograma branco simples. Refazer o Helium nesse sistema vetorial, substituindo o símbolo de música por vídeo/play com transformação/corte legível. Evitar excesso de letras, órbitas pequenas e detalhes perdidos em 16 px. Comparar também DownloadRush e UploadRush antes da execução final.

Identidade A aprovada e aplicada: SVG mestre, PNG transparente e ICO multirresolução. Manter caminho do ícone estável durante upgrades; ampliar validação em temas e escalas de tela.

## Ordem de execução e critérios

Atualização de infraestrutura (30/10/2026): migrar Actions JavaScript para Node.js 24. Aplicado em `.github/workflows/windows.yml`: checkout v7.0.1, setup-dotnet v6.0.0 e upload-artifact v7.0.2, com `runs.using: node24` conferido nas versões oficiais. Não usar a variável de compatibilidade para manter Node 20. Conferir a próxima execução e manter as Actions atualizadas. Isso não adiciona Node.js como dependência do aplicativo instalado.

Referência: https://github.blog/changelog/2025-09-19-deprecation-of-node-20-on-github-actions-runners/

1. Código Python preservado. O repositório inicialmente privado passou a público por autorização do usuário; instalador .NET já publicado após testes.
2. Criar solução .NET e migrar processamento, sondagem, cancelamento e preferências; testar mídias controladas com/sem áudio, nomes Unicode, arquivos grandes e alvos impossíveis.
3. Reproduzir as três telas em WPF e validar visualmente os estados e DPI; aplicar o ícone no padrão da família.
4. Fluxo público anônimo adotado: consulta/download/cancelamento, hashes, ferramentas em par e rollback. Não há autenticação. Ampliar testes de falhas de rede, transações e recuperação.
5. Preparar NSIS e GitHub Actions no Windows: restore/build/test/publish self-contained, obter ferramentas fixadas com hashes, compilar Setup, gerar SHA256SUMS e anexar assets à release da tag `vX.Y.Z`. Permissão contents:write apenas no job de publicação; impedir releases simultâneas para a mesma versão. Não compilar o instalador localmente como fluxo oficial.
6. Publicar versão de teste e validar upgrade real A→B com conta normal, app aberto, ferramenta ativa, falha de download, falha de troca e pins. Publicar release estável após evidências. Gerar artefatos com nome `HeliumVideoTools-X.Y.Z-win-x64-Setup.exe`.

## Estado desta entrega de planejamento

Migração .NET, novo ícone, workflow e instalador entregues. Atualização real confirmada pelo usuário. Preferências, estimativas, temas/acessibilidade, medições de desempenho, validações extensas de mídia, transações de ferramentas e pins ainda precisam de trabalho, conforme `MIGRACAO-ANDAMENTO.md`.
