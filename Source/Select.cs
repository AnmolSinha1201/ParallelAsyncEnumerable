namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<TOut>> predicate)
	{
		await foreach(var item in enumerable.SelectParallelAsync(ParallelAsyncOptions.Default(), predicate))
			yield return item;
	}

	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, ParallelAsyncOptions options, Func<TIn, ValueTask<TOut>> predicate)
	{
		var sem = new SemaphoreSlim(options.MaxDegreeOfParallelism, options.MaxDegreeOfParallelism);
		var retVal = await enumerable.Select(async item => 
		{
			await sem.WaitAsync().ConfigureAwait(false);
			var retVal = await predicate(item).ConfigureAwait(false);
			sem.Release();

			return retVal;
		}).ToListAsync(options.Token).ConfigureAwait(false);

		foreach (var item in retVal)
			yield return await item;	
	}
}