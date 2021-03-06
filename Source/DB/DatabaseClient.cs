﻿using System;
using System.Data;

using MySql.Data.MySqlClient;
using Holo;

namespace Ion.Storage
{

    public static class dataHandling
    {
        /// <summary>
        /// Converts a DataColumn to an array .
        /// </summary>
        /// <param name="dCol">The DataColumn input.</param>
        public static string[] dColToArray(DataColumn dCol)
        {
            string[] dString = new string[dCol.Table.Rows.Count];
            for (int l = 0; l < dString.Length; l++)
                dString[l] = Convert.ToString(dCol.Table.Rows[l][0]);
            return dString;
        }

        /// <summary>
        /// Converts a DataColumn to an array .
        /// </summary>
        /// <param name="dCol">The DataColumn input.</param>
        /// <param name="Tick">The output type of the array will become int.</param>
        public static int[] dColToArray(DataColumn dCol, object Tick)
        {
            int[] dInt = new int[dCol.Table.Rows.Count];
            for (int l = 0; l < dInt.Length; l++)
                dInt[l] = Convert.ToInt32(dCol.Table.Rows[l][0]);
            return dInt;
        }

    }
    /// <summary>
    /// Represents a client of a database,
    /// </summary>
    public class DatabaseClient : IDisposable
    {
        #region Fields
        private uint mHandle;
        private DateTime mLastActivity;

        private DatabaseManager mManager;
        private MySqlConnection mConnection;
        private MySqlCommand mCommand;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the handle of this database client.
        /// </summary>
        public uint Handle
        {
            get { return mHandle; }
        }
        /// <summary>
        /// Gets whether this database client is anonymous and does not recycle in the database manager.
        /// </summary>
        public bool isAnonymous
        {
            get { return (mHandle == 0); }
        }
        /// <summary>
        /// Gets the DateTime object representing the date and time this client has been used for the last time.
        /// </summary>
        public DateTime lastActivity
        {
            get { return mLastActivity; }
        }
        /// <summary>
        /// Gets the amount of seconds that this client has been inactive.
        /// </summary>
        public int Inactivity
        {
            get { return (int)(DateTime.Now - mLastActivity).TotalSeconds; }
        }
        /// <summary>
        /// Gets the state of the connection instance.
        /// </summary>
        public ConnectionState State
        {
            get { return (mConnection != null) ? mConnection.State : ConnectionState.Broken; }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Constructs a new database client with a given handle to a given database proxy.
        /// </summary>
        /// <param name="Handle">The identifier of this database client as an unsigned 32 bit integer.</param>
        /// <param name="pManager">The instance of the DatabaseManager that manages the database proxy of this database client.</param>
        public DatabaseClient(uint Handle, DatabaseManager pManager)
        {
            if (pManager == null)
                throw new ArgumentNullException("pManager");

            mHandle = Handle;
            mManager = pManager;

            mConnection = new MySqlConnection(mManager.CreateConnectionString());
            mCommand = mConnection.CreateCommand();

            UpdateLastActivity();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Attempts to open the database connection.
        /// </summary>
        public void Connect()
        {
            if (mConnection == null)
                throw new DatabaseException("Connection instance of database client " + mHandle + " holds no value.");
            if (mConnection.State != ConnectionState.Closed)
                throw new DatabaseException("Connection instance of database client " + mHandle + " requires to be closed before it can open again.");

            try
            {
                mConnection.Open();
            }
            catch (MySqlException mex)
            {
                throw new DatabaseException("Failed to open connection for database client " + mHandle + ", exception message: " + mex.Message);
            }
        }
        /// <summary>
        /// Attempts to close the database connection.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                mConnection.Close();
            }
            catch { } 
        }
        /// <summary>
        /// Closes the database connection (if open) and disposes all resources.
        /// </summary>
        public void Destroy()
        {
            Disconnect();

            mConnection.Dispose();
            mConnection = null;

            mCommand.Dispose();
            mCommand = null;

            mManager = null;
        }
        /// <summary>
        /// Updates the last activity timestamp to the current date and time.
        /// </summary>
        public void UpdateLastActivity()
        {
            mLastActivity = DateTime.Now;
        }

        /// <summary>
        /// Returns the DatabaseManager of this database client.
        /// </summary>
        public DatabaseManager GetManager()
        {
            return mManager;
        }
        /// <summary>
        /// Adds a parameter which is sql-safe to run
        /// </summary>
        /// <param name="sParam">The parameter of the value</param>
        /// <param name="val">The real value</param>
        public void AddParamWithValue(string sParam, object val)
        {
            mCommand.Parameters.AddWithValue(sParam, val);
        }

        /// <summary>
        /// This method returns the id of the last inserted id in long format, 
        /// It doesn't do an extra query to get this information (no more SELECT MAX(id) anymore)
        /// </summary>
        /// <param name="sQuery">The query</param>
        /// <returns>A long value of the inserted id</returns> 
        public long insertQuery(string sQuery)
        {
            Eucalypt.addQuery();
            long lastID;
            try
            {
                mCommand.CommandText = sQuery;
                mCommand.ExecuteScalar();
                lastID = mCommand.LastInsertedId;
                mCommand.CommandText = null;
                return lastID;
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); return 0; }
        }
        public void runQuery(string sQuery)
        {
            Eucalypt.addQuery();
            try
            {
                mCommand.CommandText = sQuery;
                mCommand.ExecuteScalar();
                long dude = mCommand.LastInsertedId;
                mCommand.CommandText = null;
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); }
        }
        /// <summary>
        /// retrieves a dataset
        /// </summary>
        public DataSet getDataSet(string sQuery)
        {
            Eucalypt.addQuery();
            DataSet pDataSet = new DataSet();
            try
            {
                mCommand.CommandText = sQuery;

                using (MySqlDataAdapter pAdapter = new MySqlDataAdapter(mCommand))
                {
                    pAdapter.Fill(pDataSet);
                }
                mCommand.CommandText = null;
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); }
            return pDataSet;
        }


        public DataTable getTable(string sQuery)
        {
            Eucalypt.addQuery();
            DataTable pDataTable = new DataTable();
            try
            {
                mCommand.CommandText = sQuery;

                using (MySqlDataAdapter pAdapter = new MySqlDataAdapter(mCommand))
                {
                    pAdapter.Fill(pDataTable);
                }
                mCommand.CommandText = null;
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); }
            return pDataTable;
        }

        
        public DataRow getRow(string sQuery)
        {
            Eucalypt.addQuery();
            DataRow dReturn = new DataTable().NewRow();

            try
            {
                mCommand.CommandText = sQuery;
                DataSet tmpSet = new DataSet();
                using (MySqlDataAdapter pAdapter = new MySqlDataAdapter(mCommand))
                {
                    pAdapter.Fill(tmpSet);
                }
                dReturn = tmpSet.Tables[0].Rows[0];
            }
            catch { }
            return dReturn;
        }


        public DataColumn getColumn(string sQuery)
        {
            Eucalypt.addQuery();
            DataColumn dReturn = new DataTable().Columns.Add();
            try
            {
                DataSet tmpSet = new DataSet();
                mCommand.CommandText = sQuery;
                using (MySqlDataAdapter pAdapter = new MySqlDataAdapter(mCommand))
                {
                    pAdapter.Fill(tmpSet);
                }
                dReturn = tmpSet.Tables[0].Columns[0];
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); }
            return dReturn;

        }

        /// <summary>
        /// method to get a string
        /// </summary>
        /// <param name="sQuery">The query</param>
        /// <returns>A String gotten from the database</returns>
        public String getString(string sQuery)
        {
            Eucalypt.addQuery();
            string pString = "";
            try
            {
                mCommand.CommandText = sQuery;
                pString = mCommand.ExecuteScalar().ToString();
                mCommand.CommandText = null;
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); }
            
            return pString;
        }
        /// <summary>
        /// returns an integer
        /// </summary>
        /// <param name="sQuery">The query</param>
        public Int32 getInt(string sQuery)
        {
            Eucalypt.addQuery();
            int i = 0;
            try
            {
                mCommand.CommandText = sQuery;
                try
                {
                    bool succes = int.TryParse(mCommand.ExecuteScalar().ToString(), out i);
                }
                catch { }
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); }
            
            return i;
        }

        public bool findsResult(string sQuery)
        {
            Eucalypt.addQuery();
            bool Found = false;
            try
            {
                mCommand.CommandText = sQuery;
                MySqlDataReader dReader = mCommand.ExecuteReader();
                Found = dReader.HasRows;
                dReader.Close();
                
            }
            catch (Exception ex) { Out.WriteError(ex.Message + "\n(^^" + sQuery + "^^)"); }
            return Found;
        }




        #region IDisposable members
        /// <summary>
        /// Returns the DatabaseClient to the DatabaseManager, where the connection will stay alive for 30 seconds of inactivity.
        /// </summary>
        public void Dispose()
        {
            if (this.isAnonymous == false) // No disposing for this client yet! Return to the manager!
            {
                // Reset this!
                mCommand.CommandText = null;
                mCommand.Parameters.Clear();

                mManager.ReleaseClient(mHandle);
            }
            else // Anonymous client, dispose this right away!
            {
                Destroy();
            }
        }
        public void resetParams()
        {
            mCommand.CommandText = null;
            mCommand.Parameters.Clear();
        }
        #endregion
        #endregion
    }
}
