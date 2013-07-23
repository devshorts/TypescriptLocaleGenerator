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


    let suffixFromName (name : string) (delim : char) = 
        name.Split(delim) |> Array.toList |> List.rev |> List.head    

    let stripSuffix (name : string) (delim : char) = 
        if not (name.Contains(delim.ToString())) then name
        else
            let removed = 
                name.Split(delim) |> Array.toList |> List.rev |> List.tail |> List.rev
                    |> List.fold (fun acc i -> acc + delim.ToString() + i) ""         

            removed.Trim(delim)
        
    let inList list item = List.exists (fun i -> i = item) list    

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

    let private rawLocaleListContains list elem = List.exists(fun j -> j.entryName = elem.entryName) list.properties   

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
    /// True if the locale's suffix is in the configs list of overridable language suffixes
    /// </summary>
    let isOverrideLanguage config locale = suffixFromName locale.targetLocale '-' |> inList config.overridableLanguageSuffixes

    /// <summary>
    /// Given two lists of properties where one is the master and one is the one you want to normalize
    /// Add to the comparer any missing methods that are in the master and not in the comparer
    /// </summary>
    let private normalizeMethods master compare = 
        let missingMethods = List.filter(fun i -> not (rawLocaleListContains compare i)) master.properties
        let methodsToExclude = List.filter(fun i -> not(rawLocaleListContains master i)) compare.properties

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
    let private normalizeExistingGroups writeGroupToFile config locales  = 
    
        seq{        
            
            let masters = masterGroups config.masterLanguage locales

            for currentLocale in locales do
                if currentLocale.targetLocale = config.masterLanguage then
                    yield currentLocale
                else                        
                    let normalizedLocaleGroups   =
                        [
                            for group in currentLocale.groups do                        
                                let ``master group`` = masters |> findGroup group.groupName

                                yield normalizeMethods ``master group`` group
                        ]
            
                    if not (isOverrideLanguage config currentLocale) then
                        normalizedLocaleGroups 
                            |> List.filter fst  // include only groups that need updating
                            |> List.map snd // select the actual groups
                            |> List.iter (writeGroupToFile config.sourceLocaleFolder currentLocale)

                    yield {
                        currentLocale with 
                            groups = List.map snd normalizedLocaleGroups
                        }                                                    
        } |> Seq.toList


    /// <summary>
    /// For any locale that is missing files, populate the files with
    /// entries from the master language and the locale list passed in
    /// and return the list of locale objects
    /// </summary>  
    let private normalizeGroups (options : GroupNormalizerOptions) =             
        seq {                 
                    
            let otherLocales = options.allLocales |> List.filter(fun i -> not (i.targetLocale = options.masterLocale.targetLocale))                                     

            for locale in otherLocales do     
                if not (options.skip locale) then
                    let ``master locale group names`` = groupNamesIn options.masterLocale

                    let ``other group names``   = groupNamesIn locale

                    let missingGroupNames =  (Set.ofSeq ``master locale group names``) - (Set.ofSeq ``other group names``) |> Set.toList

                    let groupsToDelete = (Set.ofSeq ``other group names``) - (Set.ofSeq ``master locale group names``) |> Set.toList

                    let replacedGroups = missingGroupNames |> List.map (findGroupInLocale options.masterLocale) 

                    if options.writeToFile then
                        // make sure to create any missing files
                        replacedGroups |> 
                            List.iter (writeGroupToFile options.normalizeConfig.sourceLocaleFolder locale)                
                
                    yield {
                        locale with
                            groups = List.append locale.groups replacedGroups
                    }

            if not (options.skip options.masterLocale) then
                yield options.masterLocale
        } |> Seq.toList       

    /// <summary>
    /// For any override locale that is missing files, populate the files with
    /// entries from the related master locale (so fr-FR when the override is fr-FR-LC)
    /// and return the list of locale objects
    /// </summary>  
    let private normalizeOverrideGroups locales config=
        let processOverride (localeOverride : Locale) = 
            let masterLanguage = 
                let name = stripSuffix localeOverride.targetLocale '-' 
                findLocale name locales
                                        
            let options = {
                writeToFile = false
                masterLocale = masterLanguage
                allLocales = localeOverride::[]
                normalizeConfig = config
                skip = fun input -> input.targetLocale = masterLanguage.targetLocale
            }

            normalizeGroups options
             
        // for each override, normalize it in relation to its superset parent
        // for example fr-FR-LC should be normalized to fr-FR and NOT the master
        locales 
            |> List.filter (isOverrideLanguage config)
            |> List.map processOverride
            |> List.collect id

    /// <summary>
    /// For any locale that is missing files, populate the files with
    /// entries from the english locale
    /// and return the list of locale objects
    /// </summary>  
    let private normalizeMissingGroups writeGroupToFile config locales =         
        let options = {
            writeToFile = true
            masterLocale = findLocale config.masterLanguage locales
            allLocales = locales
            normalizeConfig = config
            skip = isOverrideLanguage config
        }
                     
        let normalLanguages = normalizeGroups options

        let overrides = normalizeOverrideGroups locales config   

        List.append normalLanguages overrides
    

    /// <summary>
    /// Returns a list of localeHolder objects which represent a locale
    /// and the groups (with properties) that it has.  All locales will be normalized
    /// to English, so if any files are missing or properties are missing
    /// the objects will have english versions populated in their place, so that everyone
    /// implements all the required methods. 
    /// </summary>
    let normalize (config : NormalizerConfig) : Locale list * DelayedIO list = 
        let effects = ref []

        let ioWriter path locale group = 
            let writer = fun () -> writeGroupToFile path locale group

            effects := writer::(!effects)

            ()
   
        let locales = getLocaleAsts config.sourceLocaleFolder

        let normalizedLocales = locales 
                                    |> normalizeMissingGroups  ioWriter config
                                    |> normalizeExistingGroups ioWriter config 
                                 
        (normalizedLocales, !effects)