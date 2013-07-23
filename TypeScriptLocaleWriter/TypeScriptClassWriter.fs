namespace TypeScript

module ClassWriter = 

    open TypeScript
    open TypeScript.Data    
    open LocaleGenerator
    open LocaleGenerator.Data
    open LocaleGenerator.LocaleUtil
    open System
    open System.Globalization

    open System
    open System.IO

    
    let getClassName (locale:LocaleName) = locale.Replace("-","_").ToLowerInvariant()


    /// <summary>
    /// Takes all the classes representing this locale and merges them into one string
    /// </summary>
    let mergeClasses extractor (namespc:NameSpace) (locale:LocaleName) classes  =     
        let mergeClasses' (locale:string) namespce (classes:GeneratedClass list) = 
        
            let classString = listToString (extractor >> addNewline) classes
    
            {
                classes = List.map(fun i -> { className = i.className; 
                                              interfaceName = i.implements; 
                                              group = i.group}) classes;
                fileContents = {
                                    name = "" // not used
                                    text = classString
                               }
                locale =  locale   
            }

        mergeClasses' locale namespc classes

    /// <summary>
    /// Formats the string with a prepended capital I and title case
    /// </summary>
    let private interfaceName (i:string) : InterfaceName = "I" + (CultureInfo.InvariantCulture.TextInfo.ToTitleCase i)


    /// <summary>
    /// Wraps the text in a typescript module with a reference path to ISr.ts
    /// </summary>
    let private wrapInModuleWithSr config text = 
        String.Format(Templates.``module template``, config.``namespace``, text, config.mainInterface)

    /// <summary>
    /// Wraps the text in a typescript module without a reference path to ISr.ts
    /// </summary>
    let private wrapInModuleNoSr config text = String.Format(Templates.``module template no sr``, config.``namespace``, text)
        
    /// <summary>
    /// Takes the list of raw locale items, and an interface name 
    /// and returns the formatted typescript interface with the singature definitions as a string
    /// </summary>
    let private generateInterface (template:string) localeItems interfaceName =  
    
        let format signature =  addTabs 2 <| signature |> addSemiColon |> addNewline

        let sigs = localeItems
                        |> List.map(fun property -> Formatter.signatureFormatter property.entryName property.arguments) 
                        |> listToString format

        String.Format(template, interfaceName, sigs)    

    /// <summary>
    /// Takes the raw locale items and returns the formatted dictionary lookup for this class
    /// To be used directly in the file as a string
    /// </summary>
    let private getDictionary methods = 
        let asDictElement ``method``  = 
            let name = ``method``.entryName
            Environment.NewLine 
                |> add (addTabs 4 <| (wrapQuotes name)) 
                |> add ":  $.proxy(this."
                |> add name
                |> add ", this),"        

        let foldMethods = listToString asDictElement methods
        
        String.Format(@"
        public localeDict:{{[id:string] : (...args:any[]) => string;}} = {{}};        
    
        constructor(){{
            this.localeDict = {{
                {0}
            }}
        }}", ((foldMethods.TrimEnd()).TrimEnd([|','|])))


    /// <summary>
    /// Takes the list of (group * implements) tuples and formats the master SR interface
    /// </summary>
    let private formatSrMaster (masterInterfaceName:Name) (groupNames:GroupName list) : InterfaceImplementation = 

        let variableWithInterface = groupNames |> List.map(fun groupName -> (groupName, interfaceName groupName)) 

        let formatInterfaceElement (varName, interfaceName) = 
            (addTabs 2 <| varName) |> add ":" |> add interfaceName |> addSemiColon |> addNewline

        let implementingTypes = variableWithInterface |> listToString formatInterfaceElement 

        String.Format(Templates.masterSrInterface, 
                      implementingTypes, 
                      masterInterfaceName)

    /// <summary>
    /// Creates a class that implements SR but whos inner implmentations
    /// are references to the specific language
    /// </summary>
    let private createImplementorOf config (targetModule:GeneratedModule) = 
        let getTypeName (i:ClassInterface) = config.``namespace`` |> add "." |> add i.className 

        let formatClassEntry (``class``:ClassInterface) = 
            let typeName = getTypeName ``class``
            (addTabs 2 <| ``class``.group) 
                |> add ":" 
                |> add typeName 
                |> add " = new " 
                |> add typeName 
                |> add "();" 
                |> addNewline
    
        let impls = targetModule.classes |> listToString formatClassEntry

        let safeLocale = targetModule.locale |> getClassName

        String.Format(Templates.srImplementation, 
                      safeLocale, 
                      config.mainInterface, 
                      impls, 
                      targetModule.locale.ToLowerInvariant(), 
                      config.``namespace``)


    /// <summary>
    /// Take the raw locale methods in the object and format them as the full string representing the locale class
    /// </summary>
    let private classBody (locale:LocaleName) (``namespace``:NameSpace) group = 
    
        let className = locale |> getClassName |> add "_" |> add group.groupName

        let functions = group.properties
                            |> List.map Formatter.toFormattedLocaleMethod    
                            |> List.map(fun formattedProperty -> formattedProperty.methodBody)
                            |> listToString addNewline
   
        let implements = interfaceName group.groupName
    
        let typeScriptFunctionLookup = getDictionary group.properties

        let classText =  String.Format(Templates.``class template``, 
                                        ``namespace``, 
                                        className, 
                                        functions, 
                                        typeScriptFunctionLookup, 
                                        implements)    

        let interfaceText = generateInterface Templates.genericInterfaceDef group.properties implements

        {
            className = className;        
            fileBody = classText;
            fileInterface = interfaceText;
            implements = implements;
            group = group.groupName;
        }
    

    /// <summary>
    ///  the global interface definitions we need for stuff like "users" and "global"
    ///  that the implementtaion wrappers will be implementing. just pick any of the class implementors and extract their interfaces
    ///  since they all will implement the same thing
    /// </summary>
    let private interfacesForGroups classImplementation =        
        snd classImplementation |>
                List.map(fun impl -> impl.fileInterface)

    /// <summary>
    /// Takes all the classes for a locale
    /// merges them together and creates an implementor who points to those classes. 
    /// Returns them all as one formatted string
    /// </summary>
    let private mergeToSingleFile config (classImpl:LocaleName * GeneratedClass list) = 
        let classMerger = mergeClasses (fun i -> i.fileBody) config.``namespace``

        let (localeName, implementations) = classImpl

        let merged = classMerger localeName implementations

        let implementorOfMerged = createImplementorOf config merged

        {
            name = merged.locale |> getClassName |> add ".ts";
            text = implementorOfMerged + Environment.NewLine + merged.fileContents.text
        }
   
    /// <summary>
    /// Takes a namespace and a locale object
    /// and returns a (localeString * class implementation) tuple
    /// where the class implementation is the class text for each typescript group
    /// </summary>
    let private toClassList config locale : (LocaleName * GeneratedClass list) =     
        let implementionsPerGroup = locale.groups |> List.map (classBody locale.targetLocale config.``namespace``)

        (locale.targetLocale, implementionsPerGroup)

    /// <summary>
    /// Implements a typescript locale aggregate class that implements
    /// all the properties
    /// </summary>
    let private typeScriptClassImplementation config locale  = 
    
        locale |> toClassList config
               |> mergeToSingleFile config

    /// <summary>
    /// Finds all the interface defintions based on the implemented groups in a locale
    /// and formats it in one interface file
    /// </summary>
    let private typeScriptInterfaces config locale = 
        let groupDefs = interfacesForGroups (toClassList config locale)

        let master = LocaleNormalizer.groupsImplemented [locale] |> formatSrMaster config.mainInterface

        master::groupDefs
        

    /// <summary>
    /// Takes the raw locale files and writes a master ISr file along with each locale file
    /// To the locale target
    /// </summary>
    let write (fileWriter:FileWriter) config locales  =               

        let writeImplementation file = fileWriter config.localeTarget file.name (file.text |> wrapInModuleWithSr config)
               
        // generate the master interfaces that we need to represent these locales        
        locales
            |> LocaleUtil.findLocale config.masterLanguage
            |> typeScriptInterfaces config 
            |> listToString addNewline
            |> wrapInModuleNoSr config 
            |> fileWriter config.localeTarget (addExtension config.mainInterface "ts")
               
        locales 
            |> List.map (typeScriptClassImplementation config)                
            |> List.iter writeImplementation                  