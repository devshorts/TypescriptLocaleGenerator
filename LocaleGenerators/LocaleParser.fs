namespace LocaleGenerator

module LocaleParser = 

    open FParsec
    open System
    open LocaleGenerator.Data
    open LocaleGenerator.LocaleUtil

    exception Error of string

    (*
        About FParsec.

        The idea is to combine functions into other functions that describe the target grammar.

        For example, below are two parsers (x and y), and hte >>. operator first applies x, then 
        applies y, and returns the result of y.  

        x >>. y

        The dot indicates which parser to return. So

        x .>> y 

        returns the result of x, if x followed by y succeeds.

        The <|> operator "ors" multiple parsers, so the combinator will try each in order. The first to succeed returns

        >>? is a backtracking operator. The ? indicates that if the parser fails to backtrack the stream to the beginning
        of the parser.

        |>> pipes the result of the combinators into the type. So x |>> Text will pipe the result of x into a union type called "text"

        For more information, see the fparsec library reference
    *)

    let private asOriginalArg = function   
            | WithType(n, t) -> n + ":" + t
            | NoType(n) -> n

    let private asOriginal = function
        | Argument(a) -> "{" + asOriginalArg a + "}"
        | Text(s) -> s
        | NewLine -> Environment.NewLine + "\t= "
        | Comment(_) -> ""      

    /// <summary>
    /// Given a list of raw AST items
    /// return the original string. Used to rewrite properties
    /// in the locale group
    /// </summary>      
    let originaLines properties = 
    
        let foldBody i = i.body |> listToString asOriginal |> addNewline

        let format i = i.entryName + " = " + foldBody i
            
        properties |> listToString format    


    (*
        Utilities
    *)

    let private brackets = isAnyOf ['{';'}']
    
     (* non new line space *)  
    let private regSpace = manySatisfy (isAnyOf [' ';'\t'])
  
    (* any string literal that is charcaters *)
    let private phrase = many1Chars (satisfy (isNoneOf ['{';'\n']))
  
    let private singleWord = many1Chars (satisfy isDigit <|> satisfy isLetter <|> satisfy (isAnyOf ['_';'-']))

    (* utility method to set between parsers space agnostic *)
    let private between x y p = pstring x >>. regSpace >>. p .>> regSpace .>> pstring y

    (*
        Arguments
    *)

    let private argDelim = pstring ":"

    let private argumentNoType = singleWord |>> NoType

    let private argumentWithType = singleWord .>>.? (argDelim >>. singleWord) |>> WithType

    let private arg = (argumentWithType <|> argumentNoType) |> between "{" "}" |>> Argument

    (*
        Text Elements
    *)

    let private textElement = phrase |>> Text

    let private newLine = (unicodeNewline >>? regSpace >>? pstring "=") >>% NewLine

    let private line = many (arg <|> textElement <|> newLine)

    (*
        Entries
    *)

    let private delim = regSpace >>. pstring "=" .>> regSpace

    let private identifier = regSpace >>. singleWord .>> delim .>> regSpace

    let private localeElement = unicodeSpaces >>? (identifier .>>. line .>> skipRestOfLine true) |>> Entry

    (*
        Comments
    *)

    let private comment = pstring "#" >>. restOfLine false |>> Comment

    let private commentElement = unicodeSpaces >>? comment |>> IgnoreEntry

    (*
        Full Locale
    *)

    let private locale = many (commentElement <|> localeElement) .>> eof

    let parse input = match run locale input with
                        | Success(r,_,_) -> r
                        | Failure(r,_,_) -> 
                                Console.WriteLine r
                                raise (Error(r))
                    