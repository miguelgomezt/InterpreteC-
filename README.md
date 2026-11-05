# Intérprete en C#

Intérprete escrito en C# (.NET 10) para un lenguaje pequeño, basado en el proyecto de referencia del curso (rama `evaluator` de compi8am). Lee el código fuente, lo convierte en tokens (lexer), construye el árbol de sintaxis abstracta (parser) y lo ejecuta (evaluador).

**Curso:** Lenguajes y Compiladores — Universidad EIA

## Integrantes

| Integrante | Responsabilidad |
| --- | --- |
| Miguel Gómez T | Lexer, manejo de errores, funciones integradas y entrada (REPL/archivos) |
| Alejandro Sanchez | AST y Parser |
| Juan Jose Diaz | Evaluador, valores y ámbitos |

## Requisitos

- [SDK de .NET 10](https://dotnet.microsoft.com/download)
- Git

Verificar la instalación:

```bash
dotnet --version
```

## Instalación y compilación

```bash
git clone https://github.com/miguelgomezt/InterpreteC-.git
cd InterpreteC-
dotnet build
```

## Ejecución

**Modo interactivo (REPL):** se escribe una instrucción por línea y se ve el resultado. Las variables se conservan durante la sesión.

```bash
dotnet run --project src/Interprete
```

```text
>> let x = 2 + 3 * 4;
>> x
14
>> salir
```

**Ejecutar un archivo:**

```bash
dotnet run --project src/Interprete -- examples/variables.ssg
```

## Pruebas

Todas las pruebas (lexer, parser y evaluador) se ejecutan con un solo comando:

```bash
dotnet test
```

La carpeta `tests/` incluye entradas válidas y entradas que producen errores, cada una con su resultado esperado.

## Lenguaje soportado

| Construcción | Ejemplo |
| --- | --- |
| Variables | `let edad = 20;` |
| Asignación | `edad = edad + 1;` |
| Aritméticas | `+ - * / % **` |
| Relacionales | `== != < > <= >=` |
| Lógica | `!true` |
| Condicionales | `if (x > 5) { ... } elif (x > 0) { ... } else { ... }` |
| Ciclos | `while (i < 3) { ... }` y `for (let i = 0; i < 3; i = i + 1) { ... }` |
| Funciones | `let doble = function(x) { return x * 2; };` |
| Recursión | `let fib = function(n) { if (n <= 1) { return n; } return fib(n - 1) + fib(n - 2); };` |
| Arreglos | `let a = [1, 2, 3]; a[0]` |
| Diccionarios | `let d = {"curso": "Compiladores"}; d["curso"]` |
| Comentarios | `// esto se ignora` |
| Funciones integradas | `imprimir(x)`, `longitud(a)`, `texto(5)`, `entero("5")` |

- Solo `false` y `null` se consideran falsos.
- `+` también concatena cadenas y arreglos.

## Manejo de errores

Los errores indican la etapa y, cuando es posible, la línea y la columna:

```text
Error léxico (línea 1, columna 9): carácter no reconocido '@'
Error sintáctico (línea 2, columna 5): se esperaba ')'
Error de ejecución: variable no definida 'y'
```

## Estructura del proyecto

| Ruta | Función |
| --- | --- |
| `src/Interprete/Lexer/` | Tokens, palabras reservadas y analizador léxico |
| `src/Interprete/Ast/` | Nodos del árbol de sintaxis abstracta |
| `src/Interprete/Parser/` | Analizador sintáctico (parser de Pratt) |
| `src/Interprete/Evaluator/` | Evaluador, valores, entornos (ámbitos) y funciones integradas |
| `src/Interprete/Errors/` | Tipos de error y su reporte |
| `src/Interprete/Program.cs` | Punto de entrada: REPL y lectura de archivos |
| `tests/Interprete.Tests/` | Pruebas automáticas (xUnit) |
| `examples/` | Programas de ejemplo de cada característica |
| `docs/diseno.md` | Documento de diseño: gramática, arquitectura y decisiones |