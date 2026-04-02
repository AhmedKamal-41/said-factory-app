# Factory App - Step 1

WPF desktop application shell with navigation between MainWindow and 6 section windows.

## Build & Run

**Option 1 - Visual Studio:**
1. Open `FactoryApp.sln` in Visual Studio
2. Press F5 or click Run

**Option 2 - .NET CLI:**
```bash
cd q:\said
dotnet build FactoryApp\FactoryApp.csproj
dotnet run --project FactoryApp\FactoryApp.csproj
```

## Structure

- **MainWindow**: 6 Arabic section cards (المورد, حسابات المصنع, الخزنة, حسابات العملاء, المخزن, DDID إدخال)
- **Section windows**: Each opens on card click, has a back button (← رجوع), and returns to MainWindow
