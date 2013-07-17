namespace TypeScript

module Formatter = 

    open TypeScript.Data
    open LocaleGenerator.Data
    open LocaleGenerator.LocaleUtil
    open System
    open System.IO

    open LocaleGenerator.LocaleParser

    (*
        String formatters
    *)

    let private wrapPlus s = "+" + s.ToString()

    let private trimType item = [|item|]

    let private trimPlus (s:string) = 
        let mutable trim = s
        trim <- trim.TrimEnd(trimType '+')
        trim <- trim.TrimStart(trimType '+')
        trim

    (*
        Returns a javascript friendly argument name
    *)

    let private argumentFormatter arg = 
        match arg with
            | WithType(name, t) -> name |> add ":" |> add t
            | NoType(name) ->      name |> add ":" |> add "any"
   
    (*
        Takes the locale type and returns javascript friendly string representation
    *)

    let private contentFormatter = function
        | Argument(WithType(name, _)) -> wrapPlus (name |> add ".toString()")
        | Argument(NoType(name)) -> wrapPlus (name |> add ".toString()")
        | Text(s) -> wrapPlus(wrapQuotes s)
        | NewLine -> wrapPlus(wrapQuotes Environment.NewLine)
        | _ -> ""

    (*
        Generate a method signature given the name of the function
        and its full parsed locale body.  Will extract arguments to use them
        as the prototype
    *)

    let signatureFormatter name (arguments:Arg list) =     
        let formatter arg = argumentFormatter arg |> add ","

        let mutable args = listToString formatter arguments

        args <- args.TrimEnd([|','|])

        name 
            |> add "(" 
            |> add args 
            |> add "):string"

    (*
        Returns the method body given the the parsed locale items list
    *)

    let methodBodyFormatter signature lines =     
        let body =  listToString contentFormatter lines

        String.Format(@"
        {0}{{
            return {1};
        }}", signature, trimPlus body) 

    /// <summary>
    /// Takes a method that is a partial AST
    /// and returns a record holding the method body, signature, and function name
    ///  of this method
    /// </summary>
    let toFormattedLocaleMethod parsedMethod = 
        let entryName = parsedMethod.entryName;
        let signature = signatureFormatter parsedMethod.entryName parsedMethod.arguments
        let body = methodBodyFormatter signature parsedMethod.body
    
        {
            name = entryName;
            methodSignature = signature;
            methodBody = body
        }