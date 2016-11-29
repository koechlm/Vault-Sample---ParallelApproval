/*=====================================================================
  
  This file is part of the Autodesk Vault API Code Samples.

  Copyright (C) Autodesk Inc.  All rights reserved.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.
=====================================================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Windows.Forms;

using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;
using Forms = Autodesk.DataManagement.Client.Framework.Vault.Forms;



[assembly:ApiVersion("10.0")]
[assembly:ExtensionId("7D12A22D-F4D9-4436-B2CA-CE7FB710EC71")]

namespace RestrictOperations
{
    
    public class EventHandlers : IWebServiceExtension
    {
        private bool mFullyApproved = false;
        private String[] mParallelStates = { "geprüft Engineering", "geprüft Manufacturing", "geprüft Project Manager" };
        private long[] mApprovalStateIDs = { 48, 49, 50 };
        private long[] mReleaseStates = {51};
        private long[] mApprovalLfcycle = { 12 }; //ids of listed PW
        List<String> mReleasedFiles;
        private bool mPostRecursiv = false;

        private System.Collections.Hashtable mPWLfcycles = new System.Collections.Hashtable(); // lifecycle name = key, lifecycle ID = value
        private long[] mPWStateIDs;
        private System.Collections.Hashtable mPWStates = new System.Collections.Hashtable(); //Lifecycle ID = key, Array of State IDs = Value/Object
        //private string[] mPWStateNames;
        private System.Collections.Hashtable mPWStatesByName = new System.Collections.Hashtable(); //Lifecycle ID = key, Array of State Names = Value/Object
        private System.Collections.Hashtable mPWFinalStates = new System.Collections.Hashtable(); //Lifecycle ID = key, value = final state ID

        private List<string> m_commandList = new List<string>();

        #region IWebServiceExtension Members


        public void OnLoad()
        {
            

            // read settings
            
            // register for events here
            // in this case, we want to register for the GetRestrictions event for all operations

            // File Events
            DocumentService.AddFileEvents.GetRestrictions += new EventHandler<AddFileCommandEventArgs>(AddFileEvents_GetRestrictions);
            DocumentService.CheckinFileEvents.GetRestrictions += new EventHandler<CheckinFileCommandEventArgs>(CheckinFileEvents_GetRestrictions);
            DocumentService.CheckoutFileEvents.GetRestrictions += new EventHandler<CheckoutFileCommandEventArgs>(CheckoutFileEvents_GetRestrictions);
            DocumentService.DeleteFileEvents.GetRestrictions += new EventHandler<DeleteFileCommandEventArgs>(DeleteFileEvents_GetRestrictions);
            DocumentService.DownloadFileEvents.GetRestrictions += new EventHandler<DownloadFileCommandEventArgs>(DownloadFileEvents_GetRestrictions);
            DocumentServiceExtensions.UpdateFileLifecycleStateEvents.GetRestrictions += new EventHandler<UpdateFileLifeCycleStateCommandEventArgs>(UpdateFileLifecycleStateEvents_GetRestrictions);
            DocumentServiceExtensions.UpdateFileLifecycleStateEvents.Post += new EventHandler<UpdateFileLifeCycleStateCommandEventArgs>(UpdateFileLifecycleStateEvents_Post);

            // Folder Events
            DocumentService.AddFolderEvents.GetRestrictions += new EventHandler<AddFolderCommandEventArgs>(AddFolderEvents_GetRestrictions);
            DocumentService.DeleteFolderEvents.GetRestrictions += new EventHandler<DeleteFolderCommandEventArgs>(DeleteFolderEvents_GetRestrictions);

            // Item Events
            ItemService.AddItemEvents.GetRestrictions += new EventHandler<AddItemCommandEventArgs>(AddItemEvents_GetRestrictions);
            ItemService.CommitItemEvents.GetRestrictions += new EventHandler<CommitItemCommandEventArgs>(CommitItemEvents_GetRestrictions);
            ItemService.ItemRollbackLifeCycleStatesEvents.GetRestrictions += new EventHandler<ItemRollbackLifeCycleStateCommandEventArgs>(ItemRollbackLifeCycleStatesEvents_GetRestrictions);
            ItemService.DeleteItemEvents.GetRestrictions += new EventHandler<DeleteItemCommandEventArgs>(DeleteItemEvents_GetRestrictions);
            ItemService.EditItemEvents.GetRestrictions += new EventHandler<EditItemCommandEventArgs>(EditItemEvents_GetRestrictions);
            ItemService.PromoteItemEvents.GetRestrictions += new EventHandler<PromoteItemCommandEventArgs>(PromoteItemEvents_GetRestrictions);
            ItemService.UpdateItemLifecycleStateEvents.GetRestrictions += new EventHandler<UpdateItemLifeCycleStateCommandEventArgs>(UpdateItemLifecycleStateEvents_GetRestrictions);

            // Change Order Events
            ChangeOrderService.AddChangeOrderEvents.GetRestrictions += new EventHandler<AddChangeOrderCommandEventArgs>(AddChangeOrderEvents_GetRestrictions);
            ChangeOrderService.CommitChangeOrderEvents.GetRestrictions += new EventHandler<CommitChangeOrderCommandEventArgs>(CommitChangeOrderEvents_GetRestrictions);
            ChangeOrderService.DeleteChangeOrderEvents.GetRestrictions += new EventHandler<DeleteChangeOrderCommandEventArgs>(DeleteChangeOrderEvents_GetRestrictions);
            ChangeOrderService.EditChangeOrderEvents.GetRestrictions += new EventHandler<EditChangeOrderCommandEventArgs>(EditChangeOrderEvents_GetRestrictions);
            ChangeOrderService.UpdateChangeOrderLifecycleStateEvents.GetRestrictions += new EventHandler<UpdateChangeOrderLifeCycleStateCommandEventArgs>(UpdateChangeOrderLifecycleStateEvents_GetRestrictions);

            // Custom Entity Events
            CustomEntityService.UpdateCustomEntityLifecycleStateEvents.GetRestrictions +=new EventHandler<UpdateCustomEntityLifeCycleStateCommandEventArgs>(UpdateCustomEntityLifecycleStateEvents_GetRestrictions);
        }
        #endregion

        /// <summary>
        /// Check the lifecycles to be handled as parallel workflow
        /// </summary>
        /// <param name="mPWxml"></param>
        private void mReadPWLfcs(object sender)
        {
            XElement mXDoc = XElement.Load(RestrictOperations.RestrictSettings.GetSettingsPath("PWLifecycles.xml"));
            IEnumerable<XNode> mPWLifecycles = mXDoc.Nodes(); //all registered lifecycles with parallel workflow(s) states
            //List<string> mPWLfcNames = new List<string>(); //list the names, to get the IDs by name later
            //List<string[]> mStates = new List<string[]>(); //list the states arrays, to get their IDs later as well
            string[] mPWStateNames; //minimum 2 states for parallel workflow expected
           // List<string> mFinalStates = new List<string>(); //just one state per lifecycle; this is the state, that the final approval automatically will activate

            IWebService service = sender as IWebService;
            if (service == null)
                return;
            WebServiceCredentials cred = new WebServiceCredentials(service);
            using (WebServiceManager mgr = new WebServiceManager(cred))
            {
                long currentUserId = mgr.SecurityService.SecurityHeader.UserId;

                //int i = 0;
                foreach (XElement item in mPWLifecycles)
                {
                    //mPWLfcNames.Add(item.Attribute("Name").Value); //get all registered lifecycle names
                    LfCycDef[] defs = mgr.LifeCycleService.GetAllLifeCycleDefinitions();  //mgr.DocumentServiceExtensions.GetAllLifeCycleDefinitions();
                    //get the lifecycle object for parallel approval by name
                    LfCycDef mLfCycle = defs.FirstOrDefault(n => n.DispName == item.Attribute("Name").Value);
                    //add the Name/ID pair to the hashtable
                    if (!(mLfCycle == null))
                    {
                        mPWLfcycles.Add(item.Attribute("Name").Value, mLfCycle.Id);

                        List<XElement> mPWLfcStates = new List<XElement>();
                        Autodesk.Connectivity.WebServices.LfCycState[] mDefStates = mLfCycle.StateArray;
                        mPWLfcStates.AddRange(item.Elements("PWState"));
                        mPWStateIDs = new long[mPWLfcStates.Count];
                        mPWStateNames = new string[mPWLfcStates.Count];
                        int nStates = 0;
                        foreach (XElement mState in mPWLfcStates)
                        {
                            mPWStateNames[nStates] = mState.Attribute("Name").Value;
                            LfCycState mLfcycState = mDefStates.FirstOrDefault(x => x.DispName == mState.Attribute("Name").Value);
                            mPWStateIDs[nStates] = mLfcycState.Id;
                        
                            nStates += 1;
                        }
                        mPWStates.Add(mLfCycle.Id, mPWStateIDs);
                        mPWStatesByName.Add(mLfCycle.Id, mPWStateNames); //the history evaluation requires names instead of the IDs

                        //mFinalStates.Add(item.Element("PWFinalState").Attribute("Name").Value);
                        LfCycState mLfcycFinalState = mDefStates.FirstOrDefault(y => y.DispName == item.Element("PWFinalState").Attribute("Name").Value);
                        long[] mPWFinalStateIDs = new long[1];
                        mPWFinalStateIDs[0] = mLfcycFinalState.Id;
                        //mPWFinalStates.Add(mLfCycle.Id, mPWFinalStateIDs); //update lifecycle expects array of toIds
                        mPWFinalStates.Add(mLfCycle.Id, mLfcycFinalState.Id);

                        //  i += 1;
                    }
                }
            }//using mgr
        }

        /// <summary>
        /// Check the settings file and restrict the operation if needed.
        /// </summary>
        /// <param name="eventArgs"></param>
        private void RestrictOperation(WebServiceCommandEventArgs eventArgs, string eventName)
        {
            RestrictSettings settings = RestrictSettings.Load();
            if (settings.RestrictedOperations.Contains(eventName))
                eventArgs.AddRestriction(new ExtensionRestriction("Parallel Approval - Statuswechsel", "Folgende Vorgänger Status sind Voraussetzung zur Freigabe: " + mParallelStates[0] + ", " +
                    mParallelStates[1] + ", " + mParallelStates[2]));
        }

        private void RestrictRelease(WebServiceCommandEventArgs eventArgs, string eventName, string mFileName, string[] mStates)
        {
            string mStateNames = "";
            for (int i = 0; i < mStates.Count(); i++)
            {
                mStateNames = mStateNames + ", " + mStates[i];
            }

            eventArgs.AddRestriction(new ExtensionRestriction(mFileName, "Folgende Vorgänger Status sind Voraussetzung zur Freigabe: " + mStateNames));
        }

        void UpdateChangeOrderLifecycleStateEvents_GetRestrictions(object sender, UpdateChangeOrderLifeCycleStateCommandEventArgs e)
        {
            RestrictOperation(e, "UpdateChangeOrderLifecycleState");
        }

        void EditChangeOrderEvents_GetRestrictions(object sender, EditChangeOrderCommandEventArgs e)
        {
            RestrictOperation(e, "EditChangeOrder");
        }

        void DeleteChangeOrderEvents_GetRestrictions(object sender, DeleteChangeOrderCommandEventArgs e)
        {
            RestrictOperation(e, "DeleteChangeOrder");
        }

        void CommitChangeOrderEvents_GetRestrictions(object sender, CommitChangeOrderCommandEventArgs e)
        {
            RestrictOperation(e, "CommitChangeOrder");
        }

        void AddChangeOrderEvents_GetRestrictions(object sender, AddChangeOrderCommandEventArgs e)
        {
            RestrictOperation(e, "AddChangeOrder");
        }

        void UpdateItemLifecycleStateEvents_GetRestrictions(object sender, UpdateItemLifeCycleStateCommandEventArgs e)
        {
            RestrictOperation(e, "UpdateItemLifecycleState");
        }

        void PromoteItemEvents_GetRestrictions(object sender, PromoteItemCommandEventArgs e)
        {
            RestrictOperation(e, "PromoteItem");
        }

        void EditItemEvents_GetRestrictions(object sender, EditItemCommandEventArgs e)
        {
            RestrictOperation(e, "EditItem");
        }

        void DeleteItemEvents_GetRestrictions(object sender, DeleteItemCommandEventArgs e)
        {
            RestrictOperation(e, "DeleteItem");
        }

        void ItemRollbackLifeCycleStatesEvents_GetRestrictions(object sender, ItemRollbackLifeCycleStateCommandEventArgs e)
        {
            RestrictOperation(e, "ItemRollbackLifeCycleStates");
        }

        void CommitItemEvents_GetRestrictions(object sender, CommitItemCommandEventArgs e)
        {
            RestrictOperation(e, "CommitItem");
        }

        void AddItemEvents_GetRestrictions(object sender, AddItemCommandEventArgs e)
        {
            RestrictOperation(e, "AddItem");
        }

        void UpdateFileLifecycleStateEvents_GetRestrictions(object sender, UpdateFileLifeCycleStateCommandEventArgs e)
        {
            //PWLifecycleSettings mPWSettings = PWLifecycleSettings.Load();
            if ((mPWLfcycles.Count == 0))
            {
                mReadPWLfcs(sender);
            }

            mFullyApproved = false;
            mReleasedFiles = new List<string>();
            IWebService service = sender as IWebService;
            if (service == null)
                return;
            WebServiceCredentials cred = new WebServiceCredentials(service);
            using (WebServiceManager mgr = new WebServiceManager(cred))
            {
                long currentUserId = mgr.SecurityService.SecurityHeader.UserId;

                //FileArray mFileCollection; // = mgr.DocumentService.GetFilesByMasterIds(e.FileMasterIds);
                FileArray[] mAllFileCollections = mgr.DocumentService.GetFilesByMasterIds(e.FileMasterIds);

                int N = 0;
                foreach (FileArray mFileCollection in mAllFileCollections)
                {

                    //toDo: loop for all file collections, not only for [0]
                    int numFileCol = mFileCollection.Files.Length;

                    //check that lifecycle type is of parallelApproval ? Yes -> continue
                    File mLatestFile = mFileCollection.Files[mFileCollection.Files.Length-1];
                    long mCurrentLfcID = mLatestFile.FileLfCyc.LfCycDefId;

                    if (!(mPWLfcycles.ContainsValue(mCurrentLfcID)))
                    {
                        return;
                    }
                    if (!(mPWLfcycles.ContainsValue(mFileCollection.Files[numFileCol - 1].FileLfCyc.LfCycDefId))) //(!(mLatestFile.FileLfCyc.LfCycDefId == mLfCycle.Id))
                    {
                        return;
                    }
                    //check that current latestversion is not released state ? No -> continue
                    long mCurrentFinalStateID = (long)mPWFinalStates[mCurrentLfcID];
                    if (mLatestFile.FileLfCyc.LfCycStateId == mCurrentFinalStateID)
                    {
                        return;
                    }
                    if (mPWStates.ContainsValue(mLatestFile.FileLfCyc.LfCycStateId))
                    {
                        return;
                    }

                    //check that the file(s) toStateId is a released state or member of parallel approvals states? 
                    //Yes -> continue | NOT -> raise restriction 
                    //if (!(mApprovalStateIDs.Contains(e.ToStateIds[N])) || !(mReleaseStates.Contains(e.ToStateIds[N])))
                    //{
                    //    return;
                    //}

                    //check that the file(s) custom state is fully approved ? yes -> continue
                    long[] mReturnFiles = new long[mFileCollection.Files.Length];
                    Int32 i = 0;

                    foreach (File mFile in mFileCollection.Files)
                    {
                        mReturnFiles[i] = mFile.Id;
                        i += 1;
                    }
                    FileArray[] mFileHistory = null;
                    mFileHistory = mgr.DocumentService.GetFilesByHistoryType(mReturnFiles, FileHistoryTypeOptions.All);
                    mFullyApproved = mCheckParallelFlow(sender, e, mFileHistory, (string[])mPWStatesByName[mCurrentLfcID]);
                    //if (!(mFullyApproved) && mReleaseStates.Contains(e.ToStateIds[N]) && 
                    //    mParallelStates.Contains(mFileCollection.Files[numFileCol-2].FileLfCyc.LfCycStateName))
                    if (!(mFullyApproved) && (long)mPWFinalStates[mCurrentLfcID] == e.ToStateIds[N])
                    {
                        String mFileName = mFileCollection.Files[N].Name;
                        RestrictRelease(e, "Freigabe Bedingungen", mFileName, (string[])mPWStatesByName[mCurrentLfcID]);
                    }

                    N += 1;
                    mReturnFiles = null;
                    mFileHistory = null;
                    mLatestFile = null;

                } //foreach file collection
            } //using
        }


        private Boolean mCheckParallelFlow(object sender, UpdateFileLifeCycleStateCommandEventArgs e, FileArray[] mFileHistory, string[] mStates)
        {
            File[] mFiles = mFileHistory[0].Files;

            File maxFile = mFiles.First(n => n.MaxCkInVerNum == n.VerNum);
            if (maxFile.FileRev == null)
                return false;

            // gather all the files in the revision and arrange them by version
            IEnumerable<File> filesInRev =
                from n in mFiles
                where n.FileRev.RevId == maxFile.FileRev.RevId
                orderby n.VerNum
                select n;

            File[] mfilesArray = filesInRev.ToArray(); //mFileHistory[0].Files; ?why do we need to sort before?
            if (mfilesArray.Length >= mStates.Length)
            {
                int iF = mfilesArray.Length - 1;
                System.Collections.Hashtable mHistoryStates = new System.Collections.Hashtable();

                for (int i = 0; i < mStates.Length; i++)
                {
                    File mFile = mfilesArray[iF - i];
                    if (mStates.Contains(mFile.FileLfCyc.LfCycStateName))
                    {
                        try
                        {
                            mHistoryStates.Add(mFile.FileLfCyc.LfCycStateName, i);
                            if (mHistoryStates.Count == mStates.Length)
                            {
                                return true;
                            }
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }

                }
            }
            // if conditions before did not return already
            return false;
        }

        void UpdateFileLifecycleStateEvents_Post(object sender, UpdateFileLifeCycleStateCommandEventArgs e)
        {
            

            IWebService service = sender as IWebService;
            if (service == null)
                return;
            WebServiceCredentials cred = new WebServiceCredentials(service);
            using (WebServiceManager mgr = new WebServiceManager(cred))
            {
                long currentUserId = mgr.SecurityService.SecurityHeader.UserId;
            
                FileArray[] mAllFileCollections = mgr.DocumentService.GetFilesByMasterIds(e.FileMasterIds);
                List<long> mTempMasterIds = new List<long>();
                List<long> mTempReleaseStates = new List<long>();
                
                int mNumberOfFullyApproved = 0;
                int N = 0;
                foreach (FileArray mFileCollection in mAllFileCollections)
                {
                    int numFileCol = mFileCollection.Files.Length;
            
                    //check that lifecycle type is of parallelApproval ? Yes -> continue
                    File mLatestFile = mFileCollection.Files[mFileCollection.Files.Length - 1];
                    long mCurrentLfcID = mLatestFile.FileLfCyc.LfCycDefId;

                    if (!(mPWLfcycles.ContainsValue(mCurrentLfcID)))
                    {
                        return;
                    }
                    if (!(mPWLfcycles.ContainsValue(mFileCollection.Files[numFileCol - 1].FileLfCyc.LfCycDefId)))
                    {
                        
                        return;
                    }
                    //check that current latestversion is not released state ? 
                    //No -> continue | Yes ->  add file to the list of released files
                    long mCurrentFinalStateID = (long)mPWFinalStates[mCurrentLfcID];
                    if (mLatestFile.FileLfCyc.LfCycStateId == mCurrentFinalStateID && 
                        mFileCollection.Files[numFileCol - 1].FileRev.MaxConsumeFileId == mFileCollection.Files[numFileCol - 1].FileRev.MaxFileId)
                    {
                        String mFileName = mFileCollection.Files[0].Name;
                        mReleasedFiles.Add(mFileName);
                        mPostRecursiv = true;
                    }
                    
                    //check that the file(s) custom state is fully approved ? yes -> continue

                    long[] mReturnFiles = new long[mFileCollection.Files.Length];
                    Int32 i = 0;

                    foreach (File mFile in mFileCollection.Files)
                    {
                        mReturnFiles[i] = mFile.Id;
                        i += 1;
                    }

                    FileArray[] mFileHistory = null;
                    mFileHistory = mgr.DocumentService.GetFilesByHistoryType(mReturnFiles, FileHistoryTypeOptions.All);
                    mFullyApproved = mCheckParallelFlow(sender, e, mFileHistory, (string[])mPWStatesByName[mCurrentLfcID]);
                    
                    if (mFullyApproved && mPostRecursiv==false)
                    {
                        mTempMasterIds.Add((long)mFileCollection.Files[0].MasterId);
                        mTempReleaseStates.Add((long)mPWFinalStates[mCurrentLfcID]);
                        mNumberOfFullyApproved += 1;
                    }
                    N += 1;
                } //for each file collection

                if (mTempMasterIds.Count > 0) // && mNumberOfFullyApproved == mFileMasterIds.Length)
                {
                    long[] mFileMasterIds = new long[mTempMasterIds.Count];
                    mReleaseStates = new long[mTempReleaseStates.Count];

                    mFileMasterIds = mTempMasterIds.ToArray();
                    mReleaseStates = mTempReleaseStates.ToArray();

                    mgr.DocumentServiceExtensions.UpdateFileLifeCycleStates(mFileMasterIds, mReleaseStates, "Engineering, Manufacturing and Project Manager approved; set to fully released.");
                    mPostRecursiv = false;
                }
                
                if (mReleasedFiles.Count > 0 && N==mAllFileCollections.Length && mPostRecursiv==false)
                {
                    Form mResultForm = new mResultForm();
                    Application.EnableVisualStyles();
                    mResultForm.Show();
                    DataGridView mGrid = new DataGridView();
                    mResultForm.Controls.Add(mGrid);
                    mGrid.AutoGenerateColumns = true;
                    mGrid.ColumnCount = 1;
                    mGrid.Columns[0].Name = "Name";
                    mGrid.Columns[0].Width = 430;

                    foreach (var item in mReleasedFiles)
                    {
                        DataGridViewCell cellFileName = new DataGridViewTextBoxCell();
                        DataGridViewRow tempRow = new DataGridViewRow();
                        cellFileName.Value = item;
                        tempRow.Cells.Add(cellFileName);
                        mGrid.Rows.Add(tempRow);
                    }
                    mGrid.Width = 404;
                    mGrid.Height = 215;
                    mGrid.Dock = DockStyle.Bottom;
                    mGrid.ColumnHeadersVisible = true;
                    mGrid.EditMode = DataGridViewEditMode.EditProgrammatically;
                    mGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                    mGrid.RowHeadersWidth = 4;
                    mGrid.RowHeadersVisible = false;
                    System.Drawing.Point mPoint = new System.Drawing.Point();
                    mPoint.X = 0;
                    mPoint.Y = 55;
                    mGrid.Location = mPoint;
                }
            } // using
        }


        void DownloadFileEvents_GetRestrictions(object sender, DownloadFileCommandEventArgs e)
        {
            RestrictOperation(e, "DownloadFile");
        }

        void DeleteFolderEvents_GetRestrictions(object sender, DeleteFolderCommandEventArgs e)
        {
            RestrictOperation(e, "DeleteFolder");
        }

        void DeleteFileEvents_GetRestrictions(object sender, DeleteFileCommandEventArgs e)
        {
            RestrictOperation(e, "DeleteFile");
        }

        void CheckoutFileEvents_GetRestrictions(object sender, CheckoutFileCommandEventArgs e)
        {
            RestrictOperation(e, "CheckoutFile");
        }

        void CheckinFileEvents_GetRestrictions(object sender, CheckinFileCommandEventArgs e)
        {
            RestrictOperation(e, "CheckinFile");
        }

        void AddFolderEvents_GetRestrictions(object sender, AddFolderCommandEventArgs e)
        {
            RestrictOperation(e, "AddFolder");
        }

        void AddFileEvents_GetRestrictions(object sender, AddFileCommandEventArgs e)
        {
            RestrictOperation(e, "AddFile");
        }

        void UpdateCustomEntityLifecycleStateEvents_GetRestrictions(object sender, UpdateCustomEntityLifeCycleStateCommandEventArgs e)
        {
            RestrictOperation(e, "UpdateCustomEntityLifeCycleState");
        }
    }


}

