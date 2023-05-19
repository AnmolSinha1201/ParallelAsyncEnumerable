// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var foo =  Enumerable.Range(0, 100)
    .ToAsyncEnumerable()
    .SelectParallelAsync(async i =>
    {
        Console.WriteLine($"In Select 1 : {i}");
        await Task.Delay(1000);
        return i + 5;
    })
    .WhereParallelAsync(async i =>
    {
        Console.WriteLine($"In Where 2 : {i}");
        await Task.Delay(1000);
        return i % 2 == 0;
    });

await foreach (var item in foo)
{
    Console.WriteLine(item);
}

Console.WriteLine("Goodbye!");
