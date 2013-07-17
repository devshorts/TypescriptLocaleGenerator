namespace LocaleGenerator

open LocaleGenerator.Data

module LocaleUtil =

    let wrapQuotes s = System.String.Format("\"{0}\"", s.ToString())

    let add (i:string) (s:string) = s + i

    let addNewline i = i + System.Environment.NewLine 

    let addSemiColon s = add ";" s

    let addTabs num s = (List.init num (fun i -> "\t") 
                            |> List.fold (+) "") + s

    let listToString formatter list = List.fold(fun acc i -> acc + (formatter i)) "" list

    let addExtension (str:Path) (ext:Extension) : Path =     
        if not (System.IO.Path.GetExtension str = ext) then
            str |> add "." |> add ext
        else
            str    
                                
    /// <summary>
    /// Takes a list of locale files and finds the file that matches the passed in locale
    /// </summary>
    let findLocale locale files = 
        List.head (List.filter(fun i -> i.targetLocale = locale) files)