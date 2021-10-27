# Concise MVVM for dotnet

Concise MVVM makes it quick and easy to create view models for MVVM. In order to make MVVM work well, view models need to be able to publish state changes to 
the view. While there are many tools that help you do this it typically requires a lot of boiler plate and of repetitive work. Concise MVVM allows you to focus on the business 
logic and handles the details for you. Concise MVVM also easily integrates with INotifyPropertyChanged and INotifyCollectionChanged.

Another issue that can complicate MVVM development is publishing chages from the model layer to view models. Concise also presents a low friction toolset for doing this. 

Concise consists of a core library (**Concise**) and optional integrations:

 - **Concise** - Core Concise library
 - **Concise.Forms** - Add support Xamarin.Forms via a Page base class. (To be migrated to .NET MAUI) 
 - **Concise.Generators** - Provides a source generator to reduce boilerplate when creating view models.
 
 ## Origins
 
 Concise MVVM was developed by [humm](https://www.thinkhumm.com) (Human Universal Mind Machines, Inc.) to support their mobile products. humm contributed Concise MVVM for dotnet
to the open source community in October, 2021 to allow wider usage of this platform and encourage community support.
This project is licensed under the [MIT License](LICENSE.txt).

## Usage

The packages listed above are available as nuget packages.

## Roadmap

There is lots to be done! If you are interested in contributing, please let us know!

- **Documentation.** We need documentation of the libraries themselves, sample apps and a tutorial.
- **Mutation Persistence** The existing mutation support is in memory only. There are currently stubs for persistence that need to be implemented. 
- **Threading support.** Concise currently only supports the main thread.
