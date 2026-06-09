# Discord servers:

Velocity: 
> *https://discord.com/invite/velocityide* 

Official Custom-Uis discord server for Velocity 
> *https://discord.com/invite/r5SyNyCYjb*

includes some of the old major exploits such as but not limited to -> Synapse (X, blue, v3) KRNL, Nihon and way more to come!

## READ ME BEFORE USE!!!!! 

This is compiled for NET 10 meaning older versions will not work and will require you to manually change the version of the project. 

VelocityAPI requires **administrator privileges** to function correctly.
> If not done, your application will error out as the injector requires administrative permissions 

To force windows to prompt for administrative permission, do the following:
- Add an `app.manifest` file to your project
- Set the execution level to administrator:

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
````
> Do note that you will need to re-start Visual studio as administrator to build and debug the project from this point on. 
---

## Overview

VelocityAPI is a **.NET 10 API** used to connect applications to Velocity’s internal execution services.

It acts as a bridge between your application and Velocity’s backend systems, simplifying:

* Service communication
* Execution and injection control handling

## Installation 

### Project Reference

1. Add VelocityAPI project to your solution
2. Right-click your project → **Add Reference**
3. Select `VelocityAPI`
4. (Optional) Right click the DLL and Set `CopyAlways` to save some headaches 

## Usage Example (Mini documentation)

### Import Namespace

```csharp
using VelocityAPI;
```

---

### Create API Instance (Class Level Recommended)

```csharp
VelAPI velocityApi = new VelAPI();
```

---

### Start Communication (Window Loaded)

```csharp
private void Window_Loaded(object sender, RoutedEventArgs e)
{
    velocityApi.StartCommunication();
}
```

---

### Stop Communication (Window Closing)

```csharp
private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
{
    velocityApi.StopCommunication();
}
```

Unofficial documentation:
> *https://velodocs.vexity.shop/*
