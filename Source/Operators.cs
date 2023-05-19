public static partial class Helpers
{
	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<TOut>> predicate)
	{
		var sem = new SemaphoreSlim(10, 10);
		var retVal = await enumerable.Select(item => {
			return Task.Run(async () => {
				await sem.WaitAsync();
				var retVal = await predicate(item);
				sem.Release();

				return retVal;
			});
		}).ToListAsync();

		foreach (var item in retVal)
			yield return await item;	
	}

	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<bool>> predicate)
	{
		var sem = new SemaphoreSlim(10, 10);
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