namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<bool>> predicate)
	{
		await foreach (var item in enumerable.WhereParallelAsync(ParallelAsyncOptions.Default(), predicate))
			yield return item;
	}

	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, ParallelAsyncOptions options, Func<TIn, ValueTask<bool>> predicate)
	{
		var sem = new SemaphoreSlim(options.MaxDegreeOfParallelism, options.MaxDegreeOfParallelism);
		var retVal = await enumerable.Select(item => {
			return Task.Run(async () => {
				await sem.WaitAsync();
				var predicatedValue = await predicate(item);
				sem.Release();

				return (item, predicatedValue);
			});
		}).ToListAsync();

		foreach (Task<(TIn Item, bool PredicatedValue)> filterableTask in retVal)
		{
			var filterableItem = await filterableTask;
			if (filterableItem.PredicatedValue)
				yield return filterableItem.Item;
		}
	}
}