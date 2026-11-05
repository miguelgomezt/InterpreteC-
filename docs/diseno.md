# Documento de diseño — Intérprete en C#

**Integrantes:** Miguel Gómez T · Alejandro Sanchez · Juan Jose Diaz
**Curso:** Lenguajes y Compiladores — Universidad EIA

## 1. Arquitectura del intérprete

El intérprete procesa el programa en cuatro etapas. Cada una recibe el resultado de la anterior:

```text
Código fuente → Lexer → Tokens → Parser → AST → Evaluador → Resultado o error
```

| Componente | Carpeta | Entrada | Salida |
| --- | --- | --- | --- |
| Lexer | `Lexer/` | Texto del programa | Secuencia de `Token` |
| Parser | `Parser/` | Tokens | Árbol de sintaxis abstracta (`Program`) |
| Evaluador | `Evaluator/` | AST + entorno de variables | Valor o error de ejecución |
| REPL / archivos | `Program.cs` | Consola o archivo `.ssg` | Muestra resultados y errores |
| Errores | `Errors/` | Errores de cualquier etapa | Mensaje con tipo, línea y columna |

Las etapas se comunican solo a través de esos tipos (`Token`, nodos del AST y valores). Por eso cada parte se puede probar por separado (`LexerTests`, `ParserTests`, `EvaluatorTests`).

**Modalidad de entrada:** el programa funciona de las dos formas: REPL (`dotnet run --project src/Interprete`) y archivo (`dotnet run --project src/Interprete -- examples/variables.ssg`).

## 2. Tokens del lenguaje

| Categoría | Tokens |
| --- | --- |
| Palabras reservadas | `let` `function` `return` `if` `elif` `else` `while` `for` `true` `false` |
| Identificadores | Letra o `_` seguida de letras, dígitos o `_` (`edad`, `mi_var2`) |
| Literales | Enteros (`42`), cadenas entre comillas dobles (`"hola"`), booleanos |
| Operadores aritméticos | `+` `-` `*` `/` `%` `**` |
| Operadores relacionales | `==` `!=` `<` `>` `<=` `>=` |
| Otros operadores | `=` (asignación), `!` (negación) |
| Delimitadores | `(` `)` `{` `}` `[` `]` `,` `;` `:` |
| Especiales | `Illegal` (carácter no reconocido), `Eof` (fin de la entrada) |

**Funcionamiento del lexer:**
- Lee carácter por carácter. Con `PeekChar` mira el siguiente sin avanzar, para distinguir `=` de `==` o `*` de `**`.
- Las palabras reservadas se leen como identificadores y luego se buscan en `Keywords`. Así, agregar una palabra nueva solo requiere una línea en esa tabla.
- Cada token guarda su línea y columna para reportar errores con su posición.
- Ignora espacios, tabulaciones, saltos de línea y comentarios de una línea (`// ...`).

## 3. Gramática (EBNF)

## 4. Manejo de errores

| Etapa | Ejemplos | Dónde se detecta |
| --- | --- | --- |
| Léxico | Carácter no reconocido (`@`), cadena sin cerrar | Lexer: produce un token `Illegal` |
| Sintáctico | Token inesperado, falta `)` o `}` | Parser |
| Ejecución | Variable no definida, división por cero, tipos incompatibles | Evaluador |

Todos se representan con `InterpreterException` (tipo de error, mensaje, línea y columna). `ErrorReporter` los muestra en rojo, con la línea del código y una marca `^` en la columna del error:

```text
Error lexico (línea 2, columna 9): carácter no reconocido '@'
    let y = @;
            ^
```

El lexer no detiene la ejecución ante un error. Devuelve un token `Illegal` y el parser lo reporta, así se pueden mostrar varios errores en una sola ejecución.

## 5. Decisiones de diseño

- Se tomó como referencia la sintaxis del proyecto base (rama `evaluator`) y se portó a C#.
- `TokenType` es un `enum` (en Go eran cadenas), para que el compilador detecte tipos de token mal escritos.
- `Token` es un `record` inmutable: un token no cambia después de creado.
- Se agregaron comentarios `//`, que el proyecto base no soportaba.
- Como en el proyecto base, solo `false` y `null` son falsos. `0`, `""` y `[]` son verdaderos. La regla está en un solo lugar (`Value.IsTruthy`) y la usan `if`, `while`, `for` y `!`.
- Cada `Environment` tiene su diccionario de variables y una referencia al entorno de afuera. Para leer una variable se busca en el entorno actual y se sube hasta el global. `let` crea la variable en el ámbito actual. `=` busca hacia afuera y modifica la que ya existe. Solo las llamadas a función crean un ámbito nuevo.


## 6. Limitaciones conocidas

- Solo hay números enteros (no decimales).
- Las cadenas no admiten secuencias de escape (`\n`, `\"`).
- No hay comentarios de varias líneas.
- Los enteros son `long` de 64 bits y C# no revisa el desbordamiento. Un resultado demasiado grande da la vuelta sin aviso (por ejemplo, `2 ** 64` da `0` y `factorial(21)` da un número negativo).
- Una recursión sin caso base (o con miles de niveles) agota la pila de .NET y cierra el programa (`StackOverflowException`), que no se puede atrapar como error del intérprete.
- `if`, `while` y `for` no crean ámbito. Una variable declarada con `let` dentro de ellos (incluida la `i` de un `for`) sigue existiendo después.
- No hay `&&` ni `||`, `**` no acepta exponentes negativos, las cadenas no se comparan con `<`/`>` ni se indexan, y los arreglos se comparan por referencia (`[1] == [1]` es `false`).

## 7. Uso de inteligencia artificial

Usamos Claude (asistente de IA) como apoyo para:
- Planear la estructura del proyecto y repartir el trabajo por componentes.
- Explicar cómo portar cada parte del proyecto base de Go a C#.
- Proponer código, que cada integrante escribió y revisó en su parte.
- Encontrar y explicar errores de compilación.

**Cómo verificamos el código:**
- Compilamos con `dotnet build` después de cada cambio.
- Escribimos pruebas automáticas (`dotnet test`) con entradas válidas y entradas con error.
- Comparamos los resultados de los programas de ejemplo con los del intérprete del proyecto base.
- Revisamos el código entre nosotros (revisión cruzada), para que cada uno pueda explicar el proyecto completo.
