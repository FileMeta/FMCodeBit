# FMCodeBit
FMCodeBit is support tool for managing self-contained source code files known as 'CodeBits'. The purpose is similar to that of a package managers like [NuGet](http://www.nuget.org/) however the granularity of sharing is a single source code file. A structured comment at the beginning of the file indicates where to find the master copy so that automated tools can retrieve and update CodeBits to the latest version.

CodeBits used in this project:
- [MicroYaml](https://github.com/FileMeta/MicroYaml)
- [ConsoleHelper](https://github.com/FileMeta/ConsoleHelper)

## How to Use This Tool
**Syntax:**
FMCodeBit [options] [filenames]

Filenames may include paths and wildcards.

**Options:**
-h Present the help text
-s Search subdirectories when updating CodeBits.

**Examples:**
```
FMSrcGet -s *.cs
FMSrcGet c:\users\me\source\MyProject\MicroYaml.cs
```

For each CodeBit filename on the command line the tool reads the metadata and compares against the corresponding master copy on the web. If the master copy has a later version number then it prompts the user and then replaces the local copy with the master.

Version numbers are compared using an Alphanumeric algorithm in which sequences of digits are compared numerically while non-digit sequences are compared according to case-insensitive Unicode ordering. This comparison is well-suited to Semantic Versioning. Examples: '10.2' comes after '5.3'; '24b' comes after '8c'; and 'alpha24' comes before 'Lima10'.

## Simple CodeBit Specification
In the following text, ALL CAPS key words should be interpreted per [RFC 2119](https://tools.ietf.org/html/rfc2119).

CodeBits are source code files with a metadata block near the beginning of the file. The metadata is in [MicroYaml](https://github.com/FileMeta/MicroYaml) format (a subset of [YAML](http://www.yaml.org)) and uses metadata property definitions from [Schema.org](http://schema.org). At a minimum, the metadata block MUST include the 'url', 'version', and 'keywords' properties and 'CodeBit' MUST appear in the keywords. RECOMMENDED properties include 'name', 'description', and 'license'. Other metadata properties are optional.

The version number SHOULD use [semantic versioning](http://semver.org). 

The metadata block MUST begin with a YAML 'Begin Document' indicator which is a line with just three dashes. It MUST end with a YAML 'End Document' indicator which is a line with just three dots. Typically the metadata block is enclosed by comment delimiters appropriate to the programming language.

## Sample Metadata Block
Here is a sample metadata block for a C# source code file.

```cs
/*
---
# Metadata in YAML format (This is a YAML comment)
name: MySharedCode.cs
description: Shared code demonstration module
url: https://github.com/FileMeta/AcmeIndustries/raw/master/MySharedCode.cs
version: 1.4
keywords: CodeBit
dateModified: 2017-05-24
copyrightHolder: Henry Higgins
copyrightYear: 2017
license: https://opensource.org/licenses/BSD-3-Clause
...
*/
```

## Feature Backlog
The following features would be nice additions to FMCodeBit:
* **Dependency management:** Checks metadata for other CodeBits on which the codebit depends and automatically retrieves and/or updates those dependencies. The [schema.org SoftwareSourceCode](https://schema.org/SoftwareSourceCode) does not yet include properties to define dependencies so a new property would have to be defined. Something like 'dependsOn' with type 'URL'.
* **Semantic Versioning Sensitivity:** Rather than just getting the latest version, propose the latest version that matches the major version number. For example, version 3.4.2 can be upgraded to 3.14 without concern but an upgrade to 4.1 might require more testing because the major version change indicates that the API may not be compatible. To do this would require the tool to be able to find earlier versions on important repositories like GitHub and BitBucket.
* **Support for Languages limited to Single-Line Comments (like Python):** The metadata format depends on being able to embed multi-line comments. In languages that only support single-line comments, the tool should be able to strip a line prefix (e.g. '#' for Python) and read the balance of the line as YAML.