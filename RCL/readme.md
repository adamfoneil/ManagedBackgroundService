This has two components for monitoring background job health, used with [ManagedBackgroundServices.Abstractions](https://www.nuget.org/packages/ManagedBackgroundServices.Abstractions).

- [Dashboard](Dashboard.razor) lists registered background jobs, status, and can toggle expand recent logs for each.
- [UnhealthyServices](UnhealthyServices.razor) shows services not running