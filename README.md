<a name="readme-top"></a>
<div align="center">

# Tailwind.MSBuild

Tailwind CSS Integration for .NET Projects

<br />

<!-- PROJECT SHIELDS -->
[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

<hr />
</div>

<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#installation">Installation</a></li>
        <li><a href="#basic-use">Basic Use</a></li>
      </ul>
    </li>
    <li><a href="#customize-your-build">Customize Your Build</a></li>
    <ul>
        <li><a href="#setting-build-properties">Setting Build Properties</a></li>
        <li><a href="#executing-tasks">Executing Tasks</a></li>
    </ul>
    <li><a href="#license">License</a></li>
  </ol>
</details>

## About The Project

Tailwind.MSBuild is a NuGet package that adds MSBuild tasks for building Tailwind CSS to your project. With this package, you can easily integrate Tailwind CSS into your .NET project and automatically generate your stylesheets as part of your project's build process. It uses the [Tailwind Standalone CLI][tailwind-cli] so there are no external dependencies and you do not have to have npm installed.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Getting Started

### Installation

Use your preferred method of managing NuGet packages, such as the NuGet Package Manager within Visual Studio, or one of the following methods:

<ol>
<details>
<summary>Visual Studio</summary>

Install Tailwind.MSBuild using the NuGet Package Manager or with the following command in the Package Manager Console:

```
PM> Install-Package Tailwind.MSBuild
```

</details>
<details>
<summary>.NET CLI</summary>

You can add a package reference using the following command when the .NET CLI is available:

```
> dotnet add package Tailwind.MSBuild
```

</details>
<details>
<summary>Manually Edit Your .csproj File</summary>

> Note: You will need to build your project once in order to create the initial configuration when using this method.

You can manually add the following line to your `.csproj` file within an `ItemGroup`:

```
<PackageReference Include="Tailwind.MSBuild" Version="1.*" />
```

</details>
</ol>

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Basic Use

Once installed, you can customize Tailwind by modifying the `tailwind.config.js` and `tailwind.input.css` files in your configuration directory which defaults to `$(MSBuildProjectDirectory)\Properties\`. If the directory or either file does not exist, a default will be created for you the next time you build your project. For detailed instructions on configuring Tailwind CSS, see the official [Configuration][tailwind-docs] guide.

Each time you build your project, a css file will be generated at `$(MSBuildProjectDirectory)\wwwroot\css\tailwind.css`. You can also customize your input and output file paths. For example, to set your input file to "src/styles/tailwind.css" and your output file to "wwwroot/css/tailwind.css", you can add the following to your .csproj file:

```
<PropertyGroup Label="Tailwind Properties">
	<TailwindInputFile>$(MSBuildProjectDirectory)\src\styles\tailwind.css</TailwindInputFile>
	<TailwindOutputFile>$(MSBuildProjectDirectory)\wwwroot\css\site.css</TailwindOutputFile>
</PropertyGroup>
```

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Customize Your Build

### Setting Build Properties

If you want to change any of the default settings Tailwind.MSBuild uses, you can override them by setting any of the following properties in your `.csproj` file.

| MSBuild Property Name | Default Value                                         | Description                                                |
|-----------------------|-------------------------------------------------------|------------------------------------------------------------|
| TailwindVersion       | `latest`                                              | The version tag of the tailwind release to use.            |
| TailwindInstallPath   | `$(MSBuildThisFileDirectory)..\..\cli\`               | The directory where the tailwindcss cli should be located. |
| TailwindConfigDir     | `$(MSBuildProjectDirectory)\Properties\`              | The directory containing the tailwind configuration files. |
| TailwindConfigFile    | `tailwind.config.js`                                  | The name of the tailwind configuration file.               |
| TailwindInputFile     | `tailwind.input.css`                                  | The name of the input css file.                            |
| TailwindOutputFile    | `$(MSBuildProjectDirectory)\wwwroot\css\tailwind.css` | The path where the output css file will be located.        |
| TailwindMinify        | `false` for Debug builds, `true` for Release builds   | Whether the generated css should be minified or not.       |

>### ⚠️ *A note about `TailwindInstallPath`*:
> For the default install path, `$(MSBuildThisFileDirectory)` expands to the directory where [Tailwind.MSBuild.props][tailwind-msbuild-props] was extracted to. This means the default value of `TailwindInstallPath` is the equivilant of `{NuGetPackageCache}\tailwind.msbuild\cli\`. 

Here is a sample configuration that overrides every setting.

<ol>
<details>
<summary>Sample Config</summary>

```
<PropertyGroup Label="Tailwind Properties">
  <!-- Lock Tailwind Version -->
  <TailwindVersion>v3.2.4</TailwindVersion>
  <!-- Place Tailwind CLI in obj directory -->
  <TailwindInstallPath>$(BaseIntermediateOutputPath)\tailwind-cli\</TailwindInstallPath>
  <!-- Custom input and output file paths -->
  <TailwindConfigDir>$(MSBuildProjectDirectory)\Tailwind\</TailwindConfigDir>
  <!-- File names are relative to the TailwindConfigDir unless an absolute path is specified -->
  <TailwindConfigFile>config.js</TailwindConfigFile>
  <TailwindInputFile>input.css</TailwindInputFile>
  <TailwindOutputFile>..\wwwroot\css\site.min.css</TailwindOutputFile>
  <!-- Always minify the generated css -->
  <TailwindMinify>true</TailwindMinify>
</PropertyGroup>
```

</details>
</ol>

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Executing Tasks

[Tailwind.MSBuild.targets][tailwind-msbuild-targets] defines a build target named `BuildTailwind` that runs before the `BeforeCompile` target. This target executes two tasks that Tailwind.MSBuild implements. The first task is `GetTailwindCLI` which returns the absolute path to the tailwind CLI to the build engine. The output is stored in a property which is then passed to `BuildTailwindCSS` as the value for the `StandaloneCliPath` parameter.

For advanced scenarios where you need to run the tasks during a different point in the build process, see the tables below for the parameters required for each task and the associated MSBuild Property passed by default. It is recommended to read [Tailwind.MSBuild.targets][tailwind-msbuild-targets] as an example of how to properly invoke the MSBuild tasks.

<ol>
<details>
<summary>GetTailwindCLI</summary>

| Task Parameter  | MSBuild Property    |
|-----------------|---------------------|
| Version         | TailwindVersion     |
| RootInstallPath | TailwindInstallPath |
                
</details>
<details>
<summary>BuildTailwindCSS</summary>

| Task Parameter    | MSBuild Property           |
|-------------------|----------------------------|
| StandaloneCliPath | Output of `GetTailwindCLI` |
| WorkingDir        | TailwindConfigDir          |
| ConfigFile        | TailwindConfigFile         |
| InputFile         | TailwindInputFile          |
| OutputFile        | TailwindOutputFile         |
| Minify            | TailwindMinify             |

</details>
</ol>

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## License

Distributed under the MIT License. See [`LICENSE.md`](./LICENSE.md) for more information.

<p align="center">Copyright © 2022 Michael Rasicci</p>

<!-- MARKDOWN LINKS & IMAGES -->
[contributors-shield]: https://img.shields.io/github/contributors/mjrasicci/tailwind.msbuild.svg?style=for-the-badge
[contributors-url]: https://github.com/mjrasicci/tailwind.msbuild/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/mjrasicci/tailwind.msbuild.svg?style=for-the-badge
[forks-url]: https://github.com/mjrasicci/tailwind.msbuild/network/members
[stars-shield]: https://img.shields.io/github/stars/mjrasicci/tailwind.msbuild.svg?style=for-the-badge
[stars-url]: https://github.com/mjrasicci/tailwind.msbuild/stargazers
[issues-shield]: https://img.shields.io/github/issues/mjrasicci/tailwind.msbuild.svg?style=for-the-badge
[issues-url]: https://github.com/mjrasicci/tailwind.msbuild/issues
[license-shield]: https://img.shields.io/github/license/mjrasicci/tailwind.msbuild.svg?style=for-the-badge
[license-url]: https://github.com/mjrasicci/tailwind.msbuild/blob/master/LICENSE.txt
[tailwind-cli]: https://tailwindcss.com/blog/standalone-cli
[tailwind-docs]: https://tailwindcss.com/docs/configuration
[tailwind-msbuild-props]: ./Source/Tailwind.MSBuild/build/Tailwind.MSBuild.props
[tailwind-msbuild-targets]: ./Source/Tailwind.MSBuild/build/Tailwind.MSBuild.targets
