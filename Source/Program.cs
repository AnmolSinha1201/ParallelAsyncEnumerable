using ParallelAsyncEnumerable;

var foo =  Enumerable.Range(0, 100)
    .ToAsyncEnumerable()
    .SelectAwait(async i => {
        Console.WriteLine($"Generating {i}");
        if (i % 20 == 0)
            await Task.Delay(1000);
        return i;
    })
    .SelectParallelAsync(async i =>
    {
        Console.WriteLine($"In Select 1 : {i}");
        // Thread.Sleep(1000);
        await Task.Delay(1000);
        return i + 5;
    })
    .WhereParallelAsync(async i =>
    {
        Console.WriteLine($"In Where 2 : {i}");
        // Thread.Sleep(1000);
        await Task.Delay(1000);
        return i % 2 == 0;
    });

await foreach (var item in foo)
{
    Console.WriteLine(item);
}

Console.WriteLine("Goodbye!");
