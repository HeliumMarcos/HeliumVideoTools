# Atualizações — proposta visual

30/10/2026. Nova janela em revisão, seguindo os cards e estilos já aprovados.

A versão atual das ferramentas tem três entradas na navegação (Dividir/Compactar/Converter), sem painel de atualização. A proposta acrescenta uma janela própria, futuramente acessível por Atualizações, sem alterar os formulários existentes.

`atualizacoes-proposta.png` é uma renderização WPF em modo de prévia: não consulta a Internet nem dispara instaladores. Mostra consulta da versão estável do app, download pelo próprio programa, versões individuais do FFmpeg/ffprobe, atualização conjunta e restauração anterior. A versão 9.0.2 da imagem corresponde ao pacote preparado, mas os campos na janela real serão lidos dos binários ativos.

O repositório agora é público; não haverá campos de token/login. O instalador e o app continuam sem elevação de administrador. O progresso de download é separado do processamento de vídeos.

Status: aprovada pelo usuário em 30/10/2026 e integrada após a primeira distribuição preview.1. A segunda prévia inclui a entrada Atualizações e consulta assíncrona de versões estáveis após renderizar a janela, sem bloquear a abertura. Pré-releases não são oferecidas como atualizações estáveis.
