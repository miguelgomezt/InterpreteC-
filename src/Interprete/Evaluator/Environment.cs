// Ámbito de variables: un diccionario propio + referencia al ámbito de afuera.
namespace Interprete.Evaluation;

public class Environment
{
    private readonly Dictionary<string, Value> _store = new();
    private readonly Environment? _outer;

    // Global: new Environment().  Dentro de una función: new Environment(entornoDeLaFuncion).
    public Environment(Environment? outer = null)
    {
        _outer = outer;
    }

    // Busca aquí; si no está, pregunta al de afuera. null = no existe en ninguna parte.
    public Value? Get(string name)
    {
        if (_store.TryGetValue(name, out var value)) return value;
        return _outer?.Get(name);
    }

    // let x = ...: crea la variable en ESTE ámbito (puede tapar una de afuera con el mismo nombre).
    public Value Define(string name, Value value)
    {
        _store[name] = value;
        return value;
    }

    // x = ...: cambia la variable en el ámbito donde ya existe. false = no está definida.
    public bool Assign(string name, Value value)
    {
        if (_store.ContainsKey(name))
        {
            _store[name] = value;
            return true;
        }
        return _outer?.Assign(name, value) ?? false;
    }
}