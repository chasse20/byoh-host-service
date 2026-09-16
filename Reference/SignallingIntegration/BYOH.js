// Imports
const jwt = require( "jsonwebtoken" );
const crypto = require( "crypto" );
const axios = require( "axios" );
const logging = require( "./logging.js" );

/**
*	Static codule for specific BYOH management
*/
class BYOH
{
	/**
	*	Initializes the module by reading in configuration variables, setting up HTTP endpoints, and connecting to the Host Service
	*	@param {Object} tExpressApp ExpressJS server reference
	*	@param {Object} tConfig JSON configuration settings
	*	@param {Object} tDisconnectSessionCallback Callback for handling session disconnect
	*/
	static Initialize( tExpressApp, tConfig, tDisconnectSessionCallback )
	{
		// Read config variables
		BYOH.isUnrealConnected = false;
		BYOH.connectQuery = tConfig.connectQuery;
		BYOH.disconnectSessionQuery = tConfig.disconnectSessionQuery;
		BYOH.tokenSecret = tConfig.tokenSecret;
		BYOH.turnSecret = tConfig.turnSecret;
		BYOH.turnServer = tConfig.turnServer;
		BYOH.user = tConfig.user == null || tConfig.user === "" ? null : tConfig.user; // first user passed in when starting Cirrus
		BYOH.idleTime = tConfig.idleTime;
		BYOH.disconnectSessionCallback = tDisconnectSessionCallback;
		
		// Setup server endpoint for user
		tExpressApp.post( "/session/connect", BYOH.OnSessionConnect );
		tExpressApp.get( "/status", BYOH.OnStatus );

		// Start idle timeout if user provided
		if ( BYOH.user != null )
		{
			BYOH.ResetIdleTimer();
		}
		
		// Connect to Host Service
		BYOH.Connect();
	}
	
	/**
	*	Handler for /session/connect/:user GET endpoint; sets the user and starts the idle timer
	*	@param {Object} tRequest HTTP Request
	*	@param {Object} tResponse HTTP Response
	*/
	static OnSessionConnect( tRequest, tResponse )
	{
		if ( tRequest.connection.remoteAddress === "::1" || tRequest.connection.remoteAddress === "127.0.0.1" || tRequest.connection.remoteAddress === "::ffff:127.0.0.1" )
		{
			BYOH.user = tRequest.body.user;
			BYOH.ResetIdleTimer();
			
			console.logColor( logging.Green, "BYOH Session connected: " + BYOH.user );
			
			tResponse.sendStatus( 204 );
			return;
		}
		
		tResponse.sendStatus( 403 );
	}
	
	/**
	*	Handler for /status/ GET endpoint; returns the health status of the Signal Service and if it's connected to Unreal
	*	@param {Object} tRequest HTTP Request
	*	@param {Object} tResponse HTTP Response
	*/
	static OnStatus( tRequest, tResponse )
	{
		if ( tRequest.connection.remoteAddress === "::1" || tRequest.connection.remoteAddress === "127.0.0.1" || tRequest.connection.remoteAddress === "::ffff:127.0.0.1" )
		{
			const tempStatus =
			{
				isUnrealConnected: BYOH.isUnrealConnected
			};
			
			console.logColor( logging.Green, "BYOH Status Check: " + JSON.stringify( tempStatus ) );
			
			tResponse.status( 200 ).json( { isUnrealConnected: BYOH.isUnrealConnected } );
			return;
		}
		
		tResponse.sendStatus( 403 );
	}
	
	/**
	*	Utility function for generating the TURN/Coturn info and encrypted credentials
	*	@returns {Object} Credentials object containing the TURN/Coturn info and credentials
	*/
	static CreateTurnCredentials()
	{
		if ( BYOH.user == null )
		{
			return {};
		}
		
		const tempTime = parseInt( Date.now() / 1000 ) + 1800; // only 30 minutes allotted
		const tempUser = tempTime + ":" + BYOH.user;
		const tempEncrypted = crypto.createHmac( "sha1", BYOH.turnSecret );
		tempEncrypted.setEncoding( "base64" );
		tempEncrypted.write( tempUser );
		tempEncrypted.end();

		return {
			iceServers:
			[
				{
					urls:
					[
						"stun:" + BYOH.turnServer,
						"turn:" + BYOH.turnServer
					],
					username: tempUser,
					credential: tempEncrypted.read()
				}
			]
		};
	}
	
	/**
	*	Utility function for validating the access token contained in the /?token URI parameter
	*	@param {Object} tRequest HTTP Request
	*	@param {string} tPublicIP Public IP address used to validate against the token's audience
	*	@returns {bool} True if token is valid
	*/
	static ValidateToken( tRequest, tPublicIP )
	{
		// Reject connection if no authorization token provided
		if ( BYOH.user != null )
		{
			const tempToken = tRequest.url.substring( 8 ); // truncate /?token=
			var tempPayload = null;
			try
			{
				tempPayload = jwt.verify( tempToken, BYOH.tokenSecret, { audience: tPublicIP } );
			}
			catch ( tError )
			{
				console.log( tError );
				return false;
			}

			return tempPayload.name === BYOH.user;
		}
		
		// Allow local tests
		return tRequest.headers[ "origin" ].startsWith( "http://localhost:" );
	}
	
	/**
	*	Notifies the Host Service that this Signal Service is connected
	*/
	static Connect()
	{
		if ( BYOH.connectQuery != null )
		{
			axios.post( BYOH.connectQuery ).then( tResponse => { console.logColor( logging.Green, "Host Service connected" ); } ).catch(
				( tError ) =>
				{
					console.log( tError.response == null ? tError : tError.response.status );
				}
			);
		}
	}
	
	/**
	*	Disconnects the user, and notifies the Host Service
	*/
	static DisconnectSession()
	{
		const tempOldUser = BYOH.user;
		BYOH.user = null;
		
		clearTimeout( BYOH.idleTimer );
		BYOH.idleTimer = null;
		
		if ( BYOH.disconnectSessionQuery != null )
		{
			axios.post( BYOH.disconnectSessionQuery ).then( tResponse => { console.logColor( logging.Green, "BYOH Session disconnected: " + tempOldUser ); } ).catch(
				( tError ) =>
				{
					console.log( tError.response == null ? tError : tError.response.status );
				}
			);
		}
		
		BYOH.disconnectSessionCallback( 4001, "idle timeout" );
	}
	
	/**
	*	Resets the idle timer for users
	*/
	static ResetIdleTimer()
	{
		clearTimeout( BYOH.idleTimer );
		BYOH.idleTimer = setTimeout( BYOH.DisconnectSession, BYOH.idleTime );
	}
}

/**
*	True if the Unreal streamer socket is connected
*	@type {bool}
*/
BYOH.isUnrealConnected = false;

/**
*	Endpoint for telling the Host Service that this Signal Service is connected
*	@type {string}
*/
BYOH.connectQuery = null;

/**
*	Endpoint for telling the Host Service of a user disconnect
*	@type {string}
*/
BYOH.disconnectSessionQuery = null;

/**
*	Secret key for validating access tokens
*	@type {string}
*/
BYOH.tokenSecret = null;

/**
*	Secret key for generating access token for Coturn/TURN server
*	@type {string}
*/
BYOH.turnSecret = null;

/**
*	Public IP/URL for the TURN/Coturn server
*	@type {string}
*/
BYOH.turnServer = null;

/**
*	Current user of the Signal Service
*	@type {string}
*/
BYOH.user = null;

/**
*	Allowed idle time (ms) before a user is kicked out
*	@type {string}
*/
BYOH.idleTime = null;

/**
*	Timer for idle timeout
*	@type {string}
*/
BYOH.idleTimer = null;

/**
*	Signal Service callback for when a user is disconnects/idles out
*	@type {Object}
*/
BYOH.disconnectSessionCallback = null;

module.exports = BYOH;