module Program

open TypeScript.Data
open LocaleGenerator.Data
open LocaleGenerator
open System
open System.IO


let write dir fileName text = 
    
    let targetPath = Path.Combine(dir, fileName)

    File.WriteAllText(targetPath, text)


let executeDelayedIOWrites io = 
    io |> List.iter (fun i -> i())


[<EntryPoint>]
let main args =
    if Array.length args <> 4 then
        Console.WriteLine @"Usage: <source path for properties> <path to put generated typescript files> <typescript namespace> <main interface name>

For example: c:\website\locales c:\website\typescript\data\locales 'com.place.locale' ILocale

c:\website\locales should have folders like 'en-US' and 'fr-FR' with files of the form 'name.properties'.

An en-US folder is required since it is the master source for other properties"
        
        -1 

    else
        let localePath = args.[0]

        if not (Directory.Exists localePath) then
            Console.WriteLine("{0} doesn't exist.", localePath)

            -1
        else
            Console.WriteLine("Generating locales from {0}", localePath)

            let masterLanguage = "en-US";

            let classWriterConfig = {
                localeTarget = args.[1]                                
                ``namespace`` = args.[2]
                mainInterface = args.[3]
                masterLanguage = masterLanguage
            }

            let normalizerConfig = {
                masterLanguage = masterLanguage
                sourceLocaleFolder = localePath
                overridableLanguageSuffixes = []
            }
        
            let (normalizedLocales, fileUpdateEffects) = LocaleNormalizer.normalize normalizerConfig
        
            fileUpdateEffects |> executeDelayedIOWrites

            normalizedLocales 
                |> TypeScript.ClassWriter.write write classWriterConfig
                |> ignore
        
            0