namespace TypeScript

module Templates = 

    let ``auto generated header`` = 
        @"/**
* CLASS IS AUTO GENERATED, CHANGES WILL NOT PERSIST
*/"

    let ``module template no sr`` = 
        ``auto generated header`` + @"
/// <reference path=""../../../../shared/def/definitions.d.ts"" />

module {0} {{
    {1}
}}"

    let ``module template`` = 
        ``auto generated header`` + @"
/// <reference path=""{2}.ts""/>
/// <reference path=""../../../../shared/def/definitions.d.ts"" />

module {0} {{
    {1}
}}"


    let srImplementation = 
        @"
    export class {0} implements {4}.{1} {{
{2}        
        // locale identifier

        localeType():string{{
            return ""{3}"";
        }}
    }}
    "

    let masterSrInterface = 
        @"
    export interface {1}{{
{0}            
        localeType():string;
    }}
    "

    let genericInterfaceDef = 
        @"

    export interface {0}{{
{1}        
        localeDict:{{[id:string] : (...args:any[]) => string;}};
    }}
    "

    let ``class template`` = 
        @"
    export class {1} implements {0}.{4}{{

        // localized functions
        {2}

        // dictionary lookup
        {3}

    }}
    "
