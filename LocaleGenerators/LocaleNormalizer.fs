namespace LocaleGenerator

module LocaleNormalizer = 

    open System
    open System.IO
    open LocaleGenerator.Data
    open LocaleGenerator.LocaleUtil    
    open LocaleGenerator


    type localeAndMissingFiles = {    
        missingMethods:RawLocaleItem list;
        group: Group
    }

    let private  determineLocale file = 
        (new DirectoryInfo(file)).Name



    /// <summary>
    /// Adds a .properties to the file name if it doesn't already end with it
    /// </summary>
    let private translateToPropertiesFilename str = addExtension str "properties"

     /// <summary>
    /// Appends the methods to the file
    /// </summary>

    let private ignoreFileWriters (path:Path) locale group = ()

    let private writeGroupToFile (path:Path) locale group =
        let lines = LocaleParser.originaLines group.properties

        if not (String.IsNullOrEmpty lines) then                

            let newDir = Path.Combine(path, locale.targetLocale)

            if not (Directory.Exists (Path.GetDirectoryName newDir)) then
                Directory.CreateDirectory newDir |> ignore

            let fileName = Path.Combine(newDir, group.groupName |> translateToPropertiesFilename)
                                                
            Console.WriteLine("Updating group '{0}' for locale '{1}'", group.groupName, locale.targetLocale)

            File.WriteAllText(fileName, Environment.NewLine + lines.Trim())  
           

    /// <summary>
    /// Returns all possible locale files as a record with their 
    ///  localization properties parsed as an AST
    /// </summary>

    let private getLocaleAsts path  = 
        Seq.toList (
            seq {
                    for dir in Directory.EnumerateDirectories path do
                
                    let parsedMethods = 
                        [
                            for file in Directory.EnumerateFiles dir do
                                let properties = LocaleExtractor.getMethods file
                                yield {
                                    file = Path.GetFileName file;
                                    properties = properties;
                                    groupName = Path.GetFileNameWithoutExtension file;
                                }
                       ]

                    let locale = determineLocale dir
                    yield{
                            targetLocale = locale;
                            groups = parsedMethods
                        }
               }
        )
    
    /// <summary>
    /// Returns true if the fileMethods item (list) contains the target element
    /// </summary>

    let private listContains list elem = List.exists(fun j -> j.entryName = elem.entryName) list.properties     

    /// <summary>
    /// Returns all the group names that this locale implements
    /// </summary>
    let private  groupNamesIn locale = 
        locale.groups     
          |> Seq.map(fun i -> i.groupName)

    /// <summary> Given a name of a group and a list of groups
    /// returns the group in the list
    /// </summary>
    let private findGroup name groups = 
        groups |> List.find(fun i -> i.groupName = name)

    /// <summary>
    /// Gives you the methods that this locale implements that are part of a group
    /// I.e. if you want to get all the properties from en-US that are implemented in "global"
    /// </summary>
    let private  findGroupInLocale (locale:Locale) groupName = 
        locale.groups        
            |> List.filter(fun i -> i.groupName = groupName)        
            |> List.head
        
    /// <summary>
    /// returns the element in the group that has the most properties
    /// </summary>
    let private maxInGroup groups =
        List.maxBy(fun (a,b) -> List.length b.properties) groups


    /// <summary>
    /// Takes a list of locale items and 
    /// returns a sequence of (locale * group * methods) tuple grouped by the group
    ///  This way you can work on a group and its files per each locale
    /// </summary>
    let groupByGroup locales : seq<GroupName * seq<LocaleName * Group>> = 
        seq{
            for locale in locales do
                for group in locale.groups do
                    yield (locale.targetLocale, group)
        } |> Seq.groupBy (fun (_, name) -> name.groupName)

 
    /// <summary>
    /// Returns a string list of the groups implemented by all these files
    /// </summary>
    let groupsImplemented files = groupByGroup files 
                                    |> Seq.map fst 
                                    |> Seq.toList
    
    /// <summary>
    /// Given all the locales, return a list of all the groups that have the most methods
    /// We'll use these to normalize other groups that are missing methods
    /// </summary>

    let private masterGroups masterLanguage locales = 
        (locales |> findLocale masterLanguage).groups 

    /// <summary>
    /// Given two lists of properties where one is the master and one is the one you want to normalize
    /// Add to the comparer any missing methods that are in the master and not in the comparer
    /// </summary>
    let private normalizeMethods master compare = 
        let missingMethods = List.filter(fun i -> not (listContains compare i)) master.properties
        let methodsToExclude = List.filter(fun i -> not(listContains master i)) compare.properties

        let normalizedWithExcludes = (Set.ofList compare.properties) - (Set.ofList methodsToExclude) |> Set.toList
        let normalizedWIthIncludes = List.append normalizedWithExcludes missingMethods
         
        let needsUpdating = List.length methodsToExclude > 0 || List.length missingMethods > 0

        (needsUpdating, {
            compare with 
                properties = normalizedWIthIncludes
        })

    /// <summary>
    /// For each locales groups, fill in any missing properties from the group with the most methods
    /// </summary>
    let private normalizeExistingGroups localeDirectory writeGroupToFile masterLanguage locales  = 
    
        seq{        
            
            let masters = masterGroups masterLanguage locales

            for currentLocale in locales do
                if currentLocale.targetLocale = masterLanguage then
                    yield currentLocale
                else                        
                    let normalizedLocaleGroups   =
                        [
                            for group in currentLocale.groups do                        
                                let ``master group`` = masters |> findGroup group.groupName

                                yield normalizeMethods ``master group`` group
                        ]
            
                    normalizedLocaleGroups 
                        |> List.filter fst  // include only groups that need updating
                        |> List.map snd // select the actual groups
                        |> List.iter (writeGroupToFile localeDirectory currentLocale)

                    yield {
                        currentLocale with 
                            groups = List.map snd normalizedLocaleGroups
                        }                                                    
        } |> Seq.toList

    /// <summary>
    /// For any locale that is missing files, populate the files with
    /// entries from the english locale
    // and return the list of locale objects
    /// </summary>  
    let private normalizeMissingGroups path writeGroupToFile masterLanguage locales = 
        seq {        

            let masterLocale = findLocale masterLanguage locales 
        
            let otherLocales = List.filter(fun i -> not (i.targetLocale = masterLocale.targetLocale)) locales

            for locale in otherLocales do            
                let ``master locale group names`` = groupNamesIn masterLocale
                let ``other group names``   = groupNamesIn locale

                let missingGroupNames =  (Set.ofSeq ``master locale group names``) - (Set.ofSeq ``other group names``) 
                                              |> Set.toList

                let replacedGroups = missingGroupNames |> List.map (findGroupInLocale masterLocale) 

                // make sure to create any missing files
                replacedGroups |> List.iter (writeGroupToFile path locale)
           
                yield {
                    locale with
                        groups = List.append locale.groups replacedGroups
                }

            yield masterLocale
        } |> Seq.toList       

    /// <summary>
    /// Returns a list of localeHolder objects which represent a locale
    /// and the groups (with properties) that it has.  All locales will be normalized
    /// to English, so if any files are missing or properties are missing
    /// the objects will have english versions populated in their place, so that everyone
    /// implements all the required methods. 
    /// </summary>
    let normalize sourceFile masterLanguage : Locale list * DelayedIO list = 
        let effects = ref []

        let ioWriter path locale group = 
            let writer = fun () -> writeGroupToFile path locale group

            effects := writer::(!effects)

            ()
   
        let locales = getLocaleAsts sourceFile

        let normalizedLocales = locales 
                                    |> normalizeMissingGroups  sourceFile ioWriter masterLanguage
                                    |> normalizeExistingGroups sourceFile ioWriter masterLanguage
                                 
        (normalizedLocales, !effects)