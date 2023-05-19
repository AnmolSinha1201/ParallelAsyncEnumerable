namespace ParallelAsyncEnumerable;

public static partial class Extensions
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
}