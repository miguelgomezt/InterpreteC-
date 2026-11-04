namespace Interprete.Lexing; 

//Tipos de tokens: las "palablras" que reconoce el lenguaje 
public enum TokenType{
    Illegal, Eof, 

    //Identificadores
    Ident, Int, String,

    //Opreadores 
    Assign, Plus, Minus, Bang, Asterisk, Slash, Pow, 
    Mod, Lt, Gt, Eq, NotEq, Lte, Gte,


    //Delimitadores
    Comma, Semicolon, Colon, LParen, RParen,
    LBrace, RBrace, LBracket, RBracket,

    //Palabras reservadas
    Function, Let, True, False, If, Elif, Else, Return, For, While
}