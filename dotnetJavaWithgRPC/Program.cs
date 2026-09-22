using Grpc.Net.Client;
using GrpcCalculator;

var channel = GrpcChannel.ForAddress("http://localhost:50051");
var client = new CalculatorService.CalculatorServiceClient(channel);

var response = await client.AddAsync(new AddRequest { A = 12, B = 30 });
Console.WriteLine($"Result from Java gRPC service: {response.Result}");
