using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using Holo.Virtual.Users;
using Holo.Socketservers;
using Holo.GameConnectionSystem;
using Holo.Source.Managers;
using Ion.Storage;

namespace Holo.Source.GameConnectionSystem
{
    public class gameConnection
    {
        #region Declares
        private Socket ClientSocket;
        private byte[] dataBuffer;
        private int _ConnectionID;
        private virtualUser EndUser;
        private delegate void Handeler();
        private Handeler[] mPacket;
        private string currentPacket;
        private bool SocketClosed = false;
        private string ipNumber;
        #endregion

        #region Constructor
        public gameConnection(Socket _ClientSocket, int _ConnectionID, string ip)
        {
            this.ipNumber = ip;
            mPacket = new Handeler[2003];
            this.EndUser = new virtualUser(this);
            this.ClientSocket = _ClientSocket;
            this._ConnectionID = _ConnectionID;
            this.dataBuffer = new byte[1024];
            RegisterStandardPackets();
            this.ClientSocket.BeginReceive(this.dataBuffer, 0, this.dataBuffer.Length, SocketFlags.None, new AsyncCallback(this.dataArrival), null);
            EndUser.pingOK = true;
            sendData("@@");
        }
        #endregion

        #region Properities
        internal string connectionRemoteIP
        {
            get
            {
                return ClientSocket.RemoteEndPoint.ToString().Split(':')[0];
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Check the status of the user after an error
        /// </summary>
        /// <param name="Message">The exception the user got</param>
        internal void PreformStatusCheck(Exception Message)
        {
            try
            {
                if (this.ClientSocket.Connected == false)
                {
                    Close();
                    //Out.WriteLine("Connection" + _ConnectionID + " is dead");
                }
                else
                {
                    EndUser.sendNotify("Er is een foutje bij jou voorgekomen!\\rJe bent naar het hotel overzicht geplaatst zodat je niet opnieuw hoeft in te loggen!\\rJe fout is ook meteen opgenomen :)\\rSorry voor het ongemak");
                    EndUser.pingOK = true;
                    sendData("@@");
                    //Out.WriteLine("Connection" + _ConnectionID + " is ready for connections");
                    if (EndUser.Room != null)
                        EndUser.leaveCurrentRoom("Er is een fout bij jou gebeurd, herlaad de kamer aub!", true);
                }
            }
            catch { }
        }
        /// <summary>
        /// A new packet of data arrives
        /// </summary>
        /// <param name="iAr">The a-synch connection result</param>
        internal void dataArrival(IAsyncResult iAr)
        {
            try
            {
                int bytesReceived = new int();
                try
                {
                    bytesReceived = ClientSocket.EndReceive(iAr);
                }
                catch
                {
                    Close();
                    return;
                }
                StringBuilder DataBuffer = new StringBuilder();
                DataBuffer.Append(System.Text.Encoding.Default.GetString(dataBuffer, 0, bytesReceived));

                if (DataBuffer.ToString().Length < 3 || DataBuffer.ToString().Contains('\x01'))
                {
                    Close();
                    return;
                }

                while (DataBuffer.Length > 3)
                {

                    //Out.WriteSpecialLine(DataBuffer.ToString().Replace("\r", "{13}") , Out.logFlags.MehAction, ConsoleColor.Blue, ConsoleColor.Red, "> []", 5, ConsoleColor.Yellow);
                    int v = Encoding.decodeB64(DataBuffer.ToString().Substring(1, 2));
                    procesPacket(DataBuffer.ToString().Substring(3, v));
                    
                    DataBuffer.Remove(0, 3 + v);
                }
            }
            catch (Exception e) { 
        
        if (errorOnce)
        {
            Out.WriteDCError(e.ToString() + "\r\nRoomID = " + EndUser._roomID + "  - UserID " + EndUser._Username); PreformStatusCheck(e);
            errorOnce = false;
        }
        else
        {
            //Close();
        }
            }
            finally
            {
                try
                {
                    ClientSocket.BeginReceive(dataBuffer, 0, dataBuffer.Length, SocketFlags.None, new AsyncCallback(dataArrival), null );
                }
                catch { Close(); }
            }
        }
        bool errorOnce = true;
        /// <summary>
        /// Returns the packet to the user
        /// </summary>
        /// <returns></returns>
        internal string getPacket()
        {
            return this.currentPacket;

        }
        /// <summary>
        /// Closes the current connection, disconnecting the user
        /// </summary>
        internal void Close()
        {
            if (!SocketClosed)
            {
                SocketClosed = true;
                try
                {
                    ClientSocket.Shutdown(SocketShutdown.Both);
                    ClientSocket.Close();
                    gameSocketServer.freeConnection(_ConnectionID);
                }
                catch (Exception e) { Out.WriteDCError(e.ToString()); }
                EndUser.Reset();
            }

        }
        /// <summary>
        /// Sends data to the user
        /// </summary>
        /// <param name="Data">The data to be send</param>
        internal void sendData(string Data)
        {
            try
            {
                //Out.WriteSpecialLine(Data.Replace("\r", "{13}\r").Replace("\x2", "{2}") + "{1}", Out.logFlags.MehAction, ConsoleColor.White, ConsoleColor.DarkCyan, "> []", 2, ConsoleColor.Cyan);
                byte[] dataBytes = System.Text.Encoding.Default.GetBytes(Data + Convert.ToChar(1));
                ClientSocket.BeginSend(dataBytes, 0, dataBytes.Length, 0, new AsyncCallback(sentData), null);
            }
            catch
            {
                Close();
            }
        }
        /// <summary>
        /// Same as sendData
        /// </summary>
        /// <param name="iAr"></param>
        internal void sentData(IAsyncResult iAr)
        {
            try { ClientSocket.EndSend(iAr); }
            catch { Close(); }
        }

        /// <summary>
        /// The id of this connection
        /// </summary>
        internal int ConnectionID
        {
            get
            {
                return this._ConnectionID;
            }
        }

        /// <summary>
        /// The packet enters the method!
        /// </summary>
        /// <param name="thePacket">the packet!</param>
        internal void procesPacket(string thePacket)
        {
            currentPacket = thePacket;
            int packetID = Encoding.decodeB64(thePacket.Substring(0, 2));
            if (mPacket[packetID] != null)
            {
                //using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                //{
                //    dbClient.AddParamWithValue("packet", thePacket);
                //    dbClient.runQuery("INSERT INTO packet_log SET userid = " + this.EndUser.userID + ", packet = @packet");
                //}
                mPacket[packetID].Invoke();
            }
        }

        /// <summary>
        /// Sends an error to the user
        /// </summary>
        /// <param name="error">The error ID</param>
        internal void sendError(int error, string additionalInformation)
        {
            switch (error)
            {
                case errorList.COST_ERROR:
                    sendData("AD");
                    break;
                case errorList.USER_NOT_EXIST:
                    sendData("AL" + additionalInformation);
                    break;
                case errorList.FURNITURE_IS_IN_ROOM:
                    sendData("BKDeze meubel is al in de kamer!");
                    break;
                case errorList.PETNAME_IN_ROOM:
                    sendData("BKEr is al een huisdier met dezelfde naam in de kamer!");
                    break;
                case errorList.TOO_MUCH_PETS:
                    sendData("BKJe kan maar 3 huisdieren in je kamer hebben!");
                    break;
                case errorList.SOUNDMACHINE_IN_ROOM:
                    sendData("BKJe kan maar 1 trax-player in je kamer hebben.");
                    break;
            }

        }
        #endregion

        #region Packet registry

        /// <summary>
        /// Standard non-logged in packets
        /// </summary>
        internal void RegisterStandardPackets()
        {
            mPacket[196] = new Handeler(EndUser.pingReceived);
            mPacket[204] = new Handeler(EndUser.initializePlayer); 
            mPacket[206] = new Handeler(EndUser.cnReceived);
            mPacket[2002] = new Handeler(EndUser.packetSSO);
        }

        /// <summary>
        /// All packets for logged-in users
        /// </summary>
        internal void RegisterLoggedInPackets() 
        {
            mPacket = new Handeler[2003];

            mPacket[2] = new Handeler(EndUser.getRoomState);
            mPacket[7] = new Handeler(EndUser.refreshAppearance);
            mPacket[8] = new Handeler(EndUser.refreshValuables);
            mPacket[12] = new Handeler(EndUser.initializeMessenger);
            mPacket[15] = new Handeler(EndUser.refreshFriendlist);
            mPacket[16] = new Handeler(EndUser.getOwnRooms);
            mPacket[17] = new Handeler(EndUser.guestroomSearch);
            mPacket[18] = new Handeler(EndUser.getFavoriteRooms);
            mPacket[19] = new Handeler(EndUser.addFavouriteRooms);
            mPacket[20] = new Handeler(EndUser.removeFavouriteRoom);
            mPacket[21] = new Handeler(EndUser.getGuestroomDetails);
            mPacket[23] = new Handeler(EndUser.deleteGuestroom);
            mPacket[24] = new Handeler(EndUser.modifyRoomNameDetails);
            mPacket[25] = new Handeler(EndUser.createGuestroomPhaseTwo);
            mPacket[26] = new Handeler(EndUser.refreshClubPacket);
            mPacket[28] = new Handeler(EndUser.doTeleporterFlash);
            mPacket[29] = new Handeler(EndUser.createGuestroomPhaseOne);
            mPacket[33] = new Handeler(EndUser.sendInstantMessagetoBuddy);
            mPacket[34] = new Handeler(EndUser.massInvite);
            mPacket[37] = new Handeler(EndUser.acceptFriendRequests);
            mPacket[38] = new Handeler(EndUser.declineFriendRequests);
            mPacket[39] = new Handeler(EndUser.messengerFriendRequest);
            mPacket[40] = new Handeler(EndUser.removeBuddyFromFriendlist);
            mPacket[41] = new Handeler(EndUser.searchInConsole);
            mPacket[42] = new Handeler(EndUser.validatePetName);
            mPacket[48] = new Handeler(EndUser.pickupCFH);
            mPacket[49] = new Handeler(EndUser.dateRequest);
            mPacket[52] = new Handeler(EndUser.talkInsideRoom);
            mPacket[53] = new Handeler(EndUser.leaveCurrentRoom);
            mPacket[54] = new Handeler(EndUser.enterRoomUsingTeleporter);
            mPacket[55] = new Handeler(EndUser.talkInsideRoom);
            mPacket[56] = new Handeler(EndUser.whisperToPlayer);
            mPacket[57] = new Handeler(EndUser.enterCheckRoom);
            mPacket[59] = new Handeler(EndUser.getGuestroomData);
            mPacket[60] = new Handeler(EndUser.getRoomClass);
            mPacket[61] = new Handeler(EndUser.getRoomItems);
            mPacket[62] = new Handeler(EndUser.getGroupSkillLevelsAndGroupBadges);
            mPacket[63] = new Handeler(EndUser.getGuestroomWallItems);
            mPacket[64] = new Handeler(EndUser.enterRoom);
            mPacket[65] = new Handeler(EndUser.getHand);
            mPacket[66] = new Handeler(EndUser.applyNewDecoration);
            mPacket[67] = new Handeler(EndUser.removeItemFromRoom);
            mPacket[68] = new Handeler(EndUser.declineTrade);
            mPacket[69] = new Handeler(EndUser.acceptTrade);
            mPacket[70] = new Handeler(EndUser.abortTrade);
            mPacket[71] = new Handeler(EndUser.startTrading);
            mPacket[72] = new Handeler(EndUser.offerTradeItem);
            mPacket[73] = new Handeler(EndUser.rotateItemInRoom);
            mPacket[74] = new Handeler(EndUser.toggleFloorItemStatus);
            mPacket[75] = new Handeler(EndUser.walkToNewSquare);
            mPacket[76] = new Handeler(EndUser.spinDice);
            mPacket[77] = new Handeler(EndUser.closeDice);
            mPacket[78] = new Handeler(EndUser.openPresentBox);
            mPacket[79] = new Handeler(EndUser.rotateUser);
            mPacket[80] = new Handeler(EndUser.carryNewItem);
            mPacket[81] = new Handeler(EndUser.goEnterTeleporter);
            mPacket[83] = new Handeler(EndUser.openStickyOrPhoto);
            mPacket[84] = new Handeler(EndUser.editSticky);
            mPacket[85] = new Handeler(EndUser.deleteSticky);
            mPacket[86] = new Handeler(EndUser.sendCFH);
            mPacket[88] = new Handeler(EndUser.removeStatus);
            mPacket[90] = new Handeler(EndUser.placeItemInRoom);
            mPacket[93] = new Handeler(EndUser.doNewDance);
            mPacket[94] = new Handeler(EndUser.statusWave);
            mPacket[95] = new Handeler(EndUser.kickUserFromRoom);
            mPacket[96] = new Handeler(EndUser.giveRightsToPlayer);
            mPacket[97] = new Handeler(EndUser.removeRights);
            mPacket[98] = new Handeler(EndUser.awnserDoorbell);
            mPacket[100] = new Handeler(EndUser.doPurchase);
            mPacket[101] = new Handeler(EndUser.sendIndexPage);
            mPacket[102] = new Handeler(EndUser.openPageContent);
            mPacket[104] = new Handeler(EndUser.voteForLido);
            mPacket[105] = new Handeler(EndUser.buyIngameTickets);
            mPacket[106] = new Handeler(EndUser.dive);
            mPacket[107] = new Handeler(EndUser.splashDive);
            mPacket[115] = new Handeler(EndUser.walkToDoor);
            mPacket[116] = new Handeler(EndUser.newSwimmingOutfit);
            mPacket[126] = new Handeler(EndUser.getRoomAdvertise);
            mPacket[128] = new Handeler(EndUser.getPetInformation);
            mPacket[129] = new Handeler(EndUser.redeemCreditCode);
            mPacket[150] = new Handeler(EndUser.navigateThroughCategorie);
            mPacket[151] = new Handeler(EndUser.requestNavigatorIndex);
            mPacket[152] = new Handeler(EndUser.triggerGuestroomModify);
            mPacket[153] = new Handeler(EndUser.modifyRoomCategory);
            mPacket[154] = new Handeler(EndUser.getUserlistInsideRoom);
            mPacket[157] = new Handeler(EndUser.initializeBadges);
            mPacket[158] = new Handeler(EndUser.badgeOnOff);
            mPacket[159] = new Handeler(EndUser.refreshGameList);
            mPacket[160] = new Handeler(EndUser.getSingeleGameSub);
            mPacket[162] = new Handeler(EndUser.createNewGame);
            mPacket[163] = new Handeler(EndUser.procesNewGame);
            mPacket[165] = new Handeler(EndUser.switchTeamsInGame);
            mPacket[167] = new Handeler(EndUser.leaveGame);
            mPacket[168] = new Handeler(EndUser.kickPlayerFromGame);
            mPacket[170] = new Handeler(EndUser.startGameFromLoby);
            mPacket[171] = new Handeler(EndUser.movePlayerInGame);
            mPacket[172] = new Handeler(EndUser.proceedStartGame);
            mPacket[182] = new Handeler(EndUser.loadAdvertisement);
            mPacket[183] = new Handeler(EndUser.redeemCreditItem);
            mPacket[196] = new Handeler(EndUser.pingReceived);
            mPacket[198] = new Handeler(EndUser.moderatorDeleteCFH);
            mPacket[199] = new Handeler(EndUser.replyToCFH);
            mPacket[200] = new Handeler(EndUser.useModTool);
            mPacket[214] = new Handeler(EndUser.toggleWallItemStatus);
            mPacket[219] = new Handeler(EndUser.addSoundsetToSoundmachine);
            mPacket[220] = new Handeler(EndUser.removeSoundSetFromMachine);
            mPacket[221] = new Handeler(EndUser.getSongDetails);
            mPacket[222] = new Handeler(EndUser.recyclerSetup);
            mPacket[223] = new Handeler(EndUser.recyclerSessionStatus);
            mPacket[225] = new Handeler(EndUser.addRecyclerJob);
            mPacket[226] = new Handeler(EndUser.redeemRecyclerJob);
            mPacket[228] = new Handeler(EndUser.refreshGroup);
            mPacket[231] = new Handeler(EndUser.getGroupDetails);
            mPacket[237] = new Handeler(EndUser.receivedCFH);
            mPacket[238] = new Handeler(EndUser.deleteCFH);
            mPacket[239] = new Handeler(EndUser.initializeSoundSetPreview);
            mPacket[240] = new Handeler(EndUser.saveSoundmachineSong);
            mPacket[241] = new Handeler(EndUser.requestExistingSongSoundMachine);
            mPacket[242] = new Handeler(EndUser.saveEdittedSongSoundMachine);
            mPacket[243] = new Handeler(EndUser.savePlaylistSoundMachine);
            mPacket[244] = new Handeler(EndUser.initializeSoundMachine);
            mPacket[245] = new Handeler(EndUser.initializePlaylists);
            mPacket[247] = new Handeler(EndUser.spinWheelOfFortune);
            mPacket[248] = new Handeler(EndUser.deleteSongFromMachine);
            mPacket[254] = new Handeler(EndUser.burnSongToList);
            mPacket[261] = new Handeler(EndUser.voteForRoom);
            mPacket[262] = new Handeler(EndUser.stalkButtonMessenger);
            mPacket[263] = new Handeler(EndUser.getTagsOfUser); 
            mPacket[264] = new Handeler(EndUser.requestRandomRooms);
            mPacket[314] = new Handeler(EndUser.activateLoveSofa);
            mPacket[315] = new Handeler(EndUser.checkEventCategory);
            mPacket[317] = new Handeler(EndUser.sendSpeechBubble);
            mPacket[318] = new Handeler(EndUser.hideChatBubble);
            mPacket[320] = new Handeler(EndUser.kickAndRoomBan);
            mPacket[321] = new Handeler(EndUser.getEventsData);
            mPacket[323] = new Handeler(EndUser.getCFHroomID);
            mPacket[341] = new Handeler(EndUser.loadMoodlightSettings);
            mPacket[342] = new Handeler(EndUser.applyMoodlightSettings);
            mPacket[343] = new Handeler(EndUser.toggleMoodLight);
            mPacket[345] = new Handeler(EndUser.showEventButton);
            mPacket[346] = new Handeler(EndUser.createNewEvent);
            mPacket[347] = new Handeler(EndUser.endEvent);
            mPacket[350] = new Handeler(EndUser.openEventCategory);
            //mPacket[362] = new Handeler(EndUser.enableGuide);
            //mPacket[363] = new Handeler(EndUser.disableGuide);
            mPacket[387] = new Handeler(EndUser.playMiniGame);
            mPacket[559] = new Handeler(EndUser.editCurrentEvent);
            mPacket[770] = new Handeler(EndUser.getHand);

        }

        #endregion
    }
}
