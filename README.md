Locale-parser
=============

This is a typescript locale generator written in F# leveraging FParsec.  

Background
----
Localizing an application consists of having language files that will be used to generate strongly typed classes representing a language. The idea is to avoid hardcoding any language text in your actual application.  This project wanted to take language properties files that were of the form

```
locales/
	en-US/
	      global.properties
	      users.properties
	      storeFront.properties
	fr-FR/
	      global.properties
	      users.properties
	      storeFront.properties
```

Where each properties file will contain lines of the form

```
Property = This is some localized text
OtherProperty = This is localized text with an argument {arg:number}
ThirdProperty = This is more localized text with another argument with no type {arg}
```

The goal is to take each properties file and to create strongly typed code for each locale ending up looking like

```ts
export class enUStest implements com.devshorts.data.locale.ITest{                         
                                                                                                 
    // localized functions                                                                       
                                                                                                 
    Property():string{                                                                           
        return "This is some localized text";                                                    
    }                                                                                            
                                                                                                 
    OtherProperty(arg:number):string{                                                            
        return "This is localized text with an argument "+arg.toString();                        
    }                                                                                            
                                                                                                 
    ThirdProperty(arg:any):string{                                                               
        return "This is more localized text with another argument with no type "+arg.toString(); 
    }                                                                                            
                                                                                                 
                                                                                                 
    // dictionary lookup                                                                         
                                                                                                 
    public localeDict:{[id:string] : (...args:any[]) => string;} = {};                           
                                                                                                 
    constructor(){                                                                               
        this.localeDict = {                                                                      
                                                                                                 
			"Property":  $.proxy(this.Property, this),                                                    
			"OtherProperty":  $.proxy(this.OtherProperty, this),                                          
			"ThirdProperty":  $.proxy(this.ThirdProperty, this)                                           
        }                                                                                        
    }                                                                                            
                                                                                                 
}  
```   

With a main language wrapper that looks like. Each group (such as 'users', 'test', or 'global') gets its own class and each language gets an "implementor" class. 

```ts
module com.devshorts.data.locale {                                                                             
                                                                                                                      
    export class enUS implements com.devshorts.data.locale.ISr {                                               
		     
		test:com.devshorts.data.locale.enUStest = new com.devshorts.data.locale.enUStest();                   
                                                                                                                      
        // locale identifier                                                                                          
                                                                                                                      
        localeType():string{                                                                                          
            return "en-us";                                                                                           
        }                                                                                                             
    }                                                                                                                 
                                                                                                                      
                                                                                                                      
    export class enUStest implements com.devshorts.data.locale.ITest{   
		// ... seen above                                   
	}      
}              
```                  

There is also a master interface that you can use to set and reference whatever is the current locale.

```ts
/**
* CLASS IS AUTO GENERATED, CHANGES WILL NOT PERSIST
*/

module com.devshorts.data.locale {
    
    export interface ISr{		
		test:ITest;
            
        localeType():string;
    }
  

    export interface ITest{
		Property():string;
		OtherProperty(arg:number):string;
		ThirdProperty(arg:any):string;
        
        localeDict:{[id:string] : (...args:any[]) => string;};
    }
    

}
```              

So the final file system result might look like

```     
locales/
	en-US/
	      global.properties
	      users.properties
	      storeFront.properties
	fr-FR/
	      global.properties
	      users.properties
	      storeFront.properties
com/
	devshorts/
		data/
			locale/
				ISr.ts
				enUSLocale.ts
				frFRLocale.ts
```                           

Usage
----

Usage of the locale generator is: ```<source locale folders> <generated output folder> <namespace> <master interface name>```

So an example might be:

```>TypeScriptLocaleWriter.exe locales com/devshorts/data/locale "com.devshorts.data.locale" ISr```

In your code, you can maintain a localization singleton via a reference to the interface, such as

```ts
private sr:locale.ISr = new locale.enUsLocale();
```

Or you can do dynamic lookups of localized fields via the dictionary lookup that each group has compiled. This lets you do things like buildling AngularJS localization filters or other dynamic locale lookups.
 


Implementation
---

The locale generator is split into two sections. The first is a locale parser, which creates an AST using FParsec combinators.  The first part will also normalize all locales. For example, if you add a property to the master language (right now its "en-US" but it is configurable), you want that property to be added to the `.properties`. files of all the other languages. Same if you remove a property, you want that property to be removed. 

The second part is the typescript writer, which takes the normalized locales and their groups, generates class and interface implementations, and merges all the files into the appropriate final form.
