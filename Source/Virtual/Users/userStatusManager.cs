using System;
public class UserStatusManager
{
	/**
	 * The key/name of this status. Will be replaced by 'action' when this is a flipping status.
	 * @see action
	 */
	public string name;
	/**
	 * The value of this status. This can be omitted in some cases.
	 */
	public string data;
	
	/**
	 * A string that will hold the action to be switched with the key when this is a flipping status.
	 */
	private string action;
	/**
	 * The amount of seconds that this status last before it is switched with the action.
	 */
	private int actionSwitchSeconds;
	/**
	 * The amount of seconds that the action of this status lasts before it turns into the normal status again.
	 */
	private int actionLengthSeconds;
	/**
	 * The time in SECONDS the status switches with the action or vice versa.
	 */
	private long actionEndTime;
	/**
	 * The time in SECONDS this status ends. 0 = infinite status.
	 */
	private long statusEndTime;
	/**
	 * True if this status is currently using 'action' instead of 'name'.
	 */
	private bool actionActive;
	private bool LAST_UPDATE_CHECK;
	
	/**
	 * Constructs a new SpaceUserStatus with given data. Omit data by supplying null, not an empty string.
	 * @param name The name of the status.
	 * @param data The data of the status.
	 * @param lifeTimeSeconds The total amount of seconds this status lasts.
	 * @param action The action of the status, will be flipped with name etc.
	 * @param actionSwitchSeconds The total amount of seconds this action flips with the name.
	 * @param actionLengthSeconds The total amount of seconds that the action lasts before it flips back.
	 */
    public UserStatusManager(string name, string data, int lifeTimeSeconds, string action, int actionSwitchSeconds, int actionLengthSeconds)
    {
        long nowSeconds = DateTime.Now.Ticks / 10000;
		this.name = name;
		this.data = data;
		if(lifeTimeSeconds != 0)
			this.statusEndTime = nowSeconds + lifeTimeSeconds;
		
		if(action != null)
		{
			this.action = action;
			this.actionSwitchSeconds = actionSwitchSeconds;
			this.actionLengthSeconds = actionLengthSeconds;
			this.actionEndTime = nowSeconds + actionSwitchSeconds;
		}
    }
	
	/**
	 * Checks if the status is still valid, and returns if the check result is different from the last time.
	 * @return True if status was updated, false if it remained the same.
	 */
	public bool isUpdated()
	{
		 bool hasUpdated = false;
		 
         bool validCheckResult = this.checkStatus();
         if (validCheckResult != LAST_UPDATE_CHECK)
             hasUpdated = true; // Different result than last check!
         LAST_UPDATE_CHECK = validCheckResult;

         return hasUpdated;
	}
	
	/**
	 * Processes the status by flipping statuses and other things when it's time, and returns whether the status is still valid.
	 * @return True if status is valid, false if not. (it should be removed then!)
	 */
	public bool checkStatus()
	{
        long nowSeconds = DateTime.Now.Ticks / 10000;
		if (this.statusEndTime == 0)
            return true; // Static action, always valid
        else
        {
            if (this.statusEndTime < nowSeconds) // Non-persistent status expired
                return false;
          
        }
		
        if (this.action != null) // Status changes (eg, carry item)
        {
            if (this.actionEndTime < nowSeconds) // Status requires update
            {
            	// Swap name and action
            	string swap = this.name;
            	this.name = this.action;
            	this.action = swap;

                // Calculate new action length
                int switchSeconds = 0;
                if (this.actionActive)
                    switchSeconds = this.actionSwitchSeconds;
                else
                    switchSeconds = this.actionLengthSeconds;

                // Set new action length and force update
                this.actionActive = !this.actionActive;
                this.actionEndTime = nowSeconds + switchSeconds;
                this.LAST_UPDATE_CHECK = !this.LAST_UPDATE_CHECK;
            }
        }

        return true; // Still valid!
	}
}
