	/*
	NOTE : About launching tasks from the enumerable.
	Tasks are expensive and we might think to avoid creating them by removing the task from part of our predicate 
	and make the predicate of select to be async. ToListAsync() will as usual start the tasks and will result some finished 
	and some unfinished tasks. However, this is inadvisable as explained below.
	
	If the caller uses something like Task.Delay(), we should be fine since that unfinished task doesn't block the thread.
	The unfinished task with its SynchronizationContext is returned and ToListAsync() attaches its SynchronizationContext
	to some random thread and we can proceed.

	However, if the caller calls a blocking code such as Thread.Sleep(), it will start blocking the current thread
	and we cannot return the unfinished task and its SynchronizationContext and then we cannot attach this
	SynchronizationContext to some other thread essentially making the entire code synchronous.

	By creating tasks using Task.Run(), we essentially now block just the task threads. Since this task is still unfinished
	but are now unblocked and returnable, ToListAsync() can continue attaching SynchronizationContext to other subsequent
	task threads and launch all of them. By the time we reach the enumeration block, some of the tasks would have been
	completed and they can be enumerated. We are now forced to wait for the uncompleted tasks. 
	
	In case Thread.Sleep() or any other blocking code was called, those threads are still blocked and we wait for those 
	tasks to be completed before enumerating them. On the other hand, if non blocking code or Task.Delay was called,
	the threads are freed and we still wait for the tasks to be completed. 

	We can also avoid creating unnecessary tasks creating tasks only when the semaphore has been acquired. The amount of
	tasks created can indirectly be controlled using MaxDegreeOfParallelism. This however has an unpleasant side effect
	of chaining awaits during enumeration.
	*/




	/*
	NOTE : About SynchronizationContext (ConfigureAwait)
	We don't really care about SynchronizationContexts. 
	
	As part of WinForms, this code SHOULD NEVER be called from STAT or
	from WinFormSynchronizationContext as has been the convention since beginning. Doing so will freeze up the UI and stop
	message pump from working and make the application unresponsive. This has been the case for every method which takes
	some significant amount of time. We are not breaking the convention, it is WinForm's (and in general most UI toolkit's)
	problem. Creating threadsafe and efficient UI API code is incredibly difficult and a very-close-to-universal solution
	is to invert the problem by explicitly making UI threads and calling all other functionalities in different threads.
	Therefore, this API should also called in a similar fashion - in a non-UI thread.

	As part of legacy ASP.NET application, we need to maintain SynchronizationContexts. However, this context is supposed
	to be maintained by the caller and not the API, i.e. the caller SHOULD NOT call .ConfigureAwait(false), or better yet,
	explicitly set the synchronization contexts to be the request context. Modern ASP.NET core's SynchronizationContext
	is multithreaded and essentially stateless and in this case, we don't care about the SynchronizationContext anyway.

	Once called and the code execution is handed over to the API, we don't really care what the relevant contexts are.
	We are not utilizing any components belonging to any of the caller SynchronizationContexts and thus we can use
	whatever thread available and use SynchronizationContext.Current for further execution.

	Executing out of context of these special threads and their SynchronizationContexts also solves the deadlock issue
	where if a thread has called a .Result or any equivalent to run the API synchronously, and somewhere down the line
	if one of these special SynchronizationContexts have been captured, we can create a deadlock. Using ConfigureAwait(false)
	enables us to specify that we don't care in which SynchronizationContext the code runs in and we can pick a random thread
	from thread pool and install SynchronizationContext.Current on it and continue.

	Therefore, for the above two reasons, the library is recommended to use .ConfigureAwait(false) everywhere unless otherwise
	specified. This also provides a mild performance boost (non-goal) since we don't have to wait for a particular
	SynchronizationContext to be free and available.
	*/