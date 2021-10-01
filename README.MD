# VisualStudio 2022 debugger performance test
This simple console app is demonstrating how fast dynamic calls are in VS 2019 and VS 2022

## Testruns
I ran this app with different versions of VisualStudio and .NET versions on the same hardware. See results below:

| Configuration | .NET Version | VS Version | Debugger     | Time to complete |
| ------------- | ------------ | ---------- | ------------ | ---------------- |
| Debug         | .NET 4.8     |            | not attached | 12.98s           |
| Debug         | .NET 5       |            | not attached | 5.31s            |
| Debug         | .NET 6       |            | not attached | 5.50s            |
| ------------- | ------------ | ---------- | ------------ | ---------------- |
| Debug         | .NET 4.8     | VS 2019    | attached     | 209.78s          |
| Debug         | .NET 5       | VS 2019    | attached     | 236.72s          |
| ------------- | ------------ | ---------- | ------------ | ---------------- |
| Debug         | .NET 4.8     | VS 2022    | attached     | 135.00s          |
| Debug         | .NET 5       | VS 2022    | attached     | 155.58s          |
| Debug         | .NET 6       | VS 2022    | attached     | 156.43s          |

| Configuration | .NET Version | VS Version | Debugger     | Time to complete |
| ------------- | ------------ | ---------- | ------------ | ---------------- |
| Release       | .NET 4.8     |            | not attached | 10.43s           |
| Release       | .NET 5       |            | not attached | 5.28s            |
| Release       | .NET 6       |            | not attached | 5.22s            |
| ------------- | ------------ | ---------- | ------------ | ---------------- |
| Release       | .NET 4.8     | VS 2019    | attached     | 203.94s          |
| Release       | .NET 5       | VS 2019    | attached     | 233.74s          |
| ------------- | ------------ | ---------- | ------------ | ---------------- |
| Release       | .NET 4.8     | VS 2022    | attached     | 130.95s          |
| Release       | .NET 5       | VS 2022    | attached     | 148.64s          |
| Release       | .NET 6       | VS 2022    | attached     | 147.91s          |

