# gRPC Client

## Required package

This project uses the following NuGet packages:

- `Google.Protobuf`
- `Grpc.Net.Client`
- `Grpc.Tools`

## Run it

Run the Java service first, then execute this client:

```powershell
cd c:\Users\sbara\projects\excel
dotnet run --project dotnetJavaWithgRPC/dotnetJavaWithgRPC.csproj
```

Expected output:

```text
Result from Java gRPC service: 42
```

This client connects to the Java service in `javaAPIService` over `http://localhost:50051`.
