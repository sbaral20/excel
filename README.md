# Java API Bridge Examples

This workspace contains three project variants that call the same Java method, `customAdd(int a, int b)`, exposed by the Java interface in `javaAPIs`.

## Projects

- `javaAPIs` — shared Java API project
- `dotNetJavaWithJNI` — .NET to Java via JNI
- `dotNetJavaWithkvm` — .NET to Java via IKVM
- `javaAPIService` — Java gRPC service implementation
- `dotnetJavaWithgRPC` — .NET gRPC client

## Package requirements by project

### `javaAPIs`
- Maven packages:
  - `org.junit.jupiter:junit-jupiter` (test scope)
- Java version: 8-compatible bytecode

### `dotNetJavaWithJNI`
- NuGet packages:
  - `Microsoft.NET.Test.Sdk`
  - `xunit`
  - `xunit.runner.visualstudio`
  - `coverlet.collector`
- Requires `JAVA_HOME` to point to a valid JDK installation
- Uses `jvm.dll` via JNI

### `dotNetJavaWithkvm`
- NuGet packages:
  - `IKVM`
  - `Microsoft.NET.Test.Sdk`
  - `xunit`
  - `xunit.runner.visualstudio`
  - `coverlet.collector`
- Requires the Java API jar to be built first

### `javaAPIService`
- Maven packages:
  - `io.grpc:grpc-netty-shaded`
  - `io.grpc:grpc-protobuf`
  - `io.grpc:grpc-stub`
  - `com.google.protobuf:protobuf-java`
  - `javax.annotation:javax.annotation-api`
  - `kr.motd.maven:os-maven-plugin`
  - `org.xolstice.maven.plugins:protobuf-maven-plugin`

### `dotnetJavaWithgRPC`
- NuGet packages:
  - `Google.Protobuf`
  - `Grpc.Net.Client`
  - `Grpc.Tools`
- Connects to the Java gRPC service at `http://localhost:50051`

## 1) Shared Java API build

```powershell
cd c:\Users\sbara\projects\excel
mvn -q -f javaAPIs/pom.xml clean package
```

## 2) Run the JNI bridge

```powershell
cd c:\Users\sbara\projects\excel
dotnet test dotNetJavaWithJNI/dotNetJavaWithJNI.csproj --nologo
```

## 3) Run the IKVM bridge

```powershell
cd c:\Users\sbara\projects\excel
dotnet test dotNetJavaWithkvm/dotNetJavaWithkvm.csproj --nologo
```

## 4) Run the Java gRPC service

Start the Java service in one terminal:

```powershell
cd c:\Users\sbara\projects\excel
mvn -q -f javaAPIService/pom.xml exec:java -Dexec.mainClass=com.example.grpc.JavaApiServiceServer
```

If the plugin is not available, build once first:

```powershell
cd c:\Users\sbara\projects\excel
mvn -q -f javaAPIService/pom.xml package
```

Then run the service:

```powershell
cd c:\Users\sbara\projects\excel
java -cp javaAPIService/target/classes;javaAPIService/target/generated-sources/protobuf/java;javaAPIService/target/generated-sources/protobuf/grpc-java;javaAPIService/target/* com.example.grpc.JavaApiServiceServer
```

## 5) Run the .NET gRPC client

In a second terminal:

```powershell
cd c:\Users\sbara\projects\excel
dotnet run --project dotnetJavaWithgRPC/dotnetJavaWithgRPC.csproj
```

This calls the Java gRPC service on `http://localhost:50051` and prints the result of `12 + 30`.

## Notes

- The Java service and the Java API project both use Java 8-compatible bytecode for IKVM compatibility.
- The project paths are relative and portable to the workspace.
