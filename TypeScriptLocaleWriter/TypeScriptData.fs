namespace TypeScript

open LocaleGenerator.Data

module Data = 

    type InterfaceImplementation = string

    type ClassImplementation = string

    type InterfaceName = string

    type NameSpace = string

    type ArgumentFormatter = Arg -> string

    type LocalizedLineFormatter = LocaleContents -> string

    type MethodBodyFormatter = string -> LocaleContents list -> string

    type SignatureFormatter = Name -> Arg list -> string

    type FileWriter = Path -> string -> string -> unit
    
    type FormattedLocaleMethod = {
        name : string;
        methodSignature: string;
        methodBody : string
    }

    type FileNameAndContents = {
        name:string;
        text:string;
    }

    type ClassInterface = {
        className:string;
        interfaceName:string;
        group:string;
    }
    
    type GeneratedClass = {
        className:string;    
        fileBody:ClassImplementation;
        fileInterface:InterfaceImplementation;
        implements:string;
        group:GroupName;
    }   

    type GeneratedModule = {
        classes:ClassInterface list
        fileContents: FileNameAndContents
        locale:LocaleName;    
    }

    type WriterConfig = {
        localeTarget:string;
        mainInterface:string;
        masterLanguage:string;
        ``namespace``:string;
    }