namespace LocaleGenerator

module Data =

    type DelayedIO = unit -> unit

    type GroupName = string

    type LocaleName = string

    type Path = string

    type Extension = string

    type Name = string

    type Type = string

    type Arg = 
        | WithType of Name * Type
        | NoType of Name

    type LocaleContents = 
        | Argument of Arg
        | Text of string    
        | Comment of string
        | NewLine

    type LocaleProperty = (string * LocaleContents list)

    type LocaleElement =
        | Entry of LocaleProperty
        | IgnoreEntry of LocaleContents

    type RawLocaleItem = {
        entryName: Name;
        arguments: Arg list;
        body: LocaleContents list;
    }           

    type Group = {
        file: Name;
        properties: RawLocaleItem list
        groupName: GroupName;
    }

    type Locale = {
        targetLocale: LocaleName;
        groups: Group list
    }