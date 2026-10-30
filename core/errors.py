class FluxoError(RuntimeError):
    """Erro esperado, adequado para apresentação ao usuário."""


class TaskCancelled(FluxoError):
    pass


class DependencyError(FluxoError):
    pass
