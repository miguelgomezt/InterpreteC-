// Función definida por el usuario: parámetros + cuerpo + el entorno donde se creó (closure).
using Interprete.Ast;

namespace Interprete.Evaluation;

public sealed record FunctionValue(
    IReadOnlyList<Identifier> Parameters,
    BlockStmt Body,
    Environment Env) : Value
{
    public override string TypeName => "FUNCION";

    public override string Inspect() =>
        $"function({string.Join(", ", Parameters)}) {Body}";
}