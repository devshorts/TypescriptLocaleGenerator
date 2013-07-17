namespace LocaleGenerator

module LocaleExtractor =

    open LocaleGenerator.Data
    open System
    open System.IO

    open LocaleParser


    /// <summary>
    ///  Returns true if the current locale contents item is an argument
    /// </summary>

    let private isArg (arg:LocaleContents) = 
        match arg with 
            | Argument(_) -> true
            | _ -> false
  
    /// <summary>
    ///  Extract the inner Arg type out of a wrapped Argument type
    /// </summary>

    let private asArg (arg:LocaleContents) = 
        match arg with 
            | Argument(a) -> a        
            | _ -> raise (Error("Could not extract argument from a non argument type"))

    /// <summary>
    ///  Select only a parsed locale lines arguments
    /// </summary>

    let private getArgs line = List.filter(isArg) line

    /// <summary>
    ///  Generate a method signature given the name of the function
    ///  and its full parsed locale body.  Will extract arguments to use them
    ///  as the prototype
    /// <summary>

    let private entryArgs list =
         List.map(asArg) (List.filter(isArg) list)


    /// <summary>
    ///  Skips all elements with comments
    /// </summary>

    let private getNameList entry =
        match entry with 
            | Entry(name, contents) -> (name, contents)
            | IgnoreEntry(_) -> raise (Error("Should not have been a comment"))


    /// <summary>
    ///  Takes the method name and the localed items AST
    ///  and returns the full method 
    /// </summary>

    let private localizedMethod entry = 
        match entry with 
            | Entry(name, parsedElements) -> 
                    let title = name

                    let argList = entryArgs parsedElements

                    let body = parsedElements

                    {
                        entryName = title;
                        arguments = argList;
                        body = body
                    }|> Some
            | _ -> None

    /// <summary>
    ///  Returns all the localizable methods as a string
    /// </summary>

    let getMethods file  = 
        if not (File.Exists file) then
            raise (Error(file + " does not exist, cannot extract properties"))

        let contents = (File.ReadAllText file).Trim()

        let entries = parse contents
    
        List.map(localizedMethod) entries 
            |> List.filter Option.isSome 
            |> List.map Option.get    