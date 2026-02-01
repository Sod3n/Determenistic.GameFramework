var SignalRLib = {
    $vars: {
        connection: null,
        lastConnectionId: '',
        connectedCallback: null,
        disconnectedCallback: null,
        handlerCallback1: null,
        handlerCallback2: null,
        handlerCallback3: null,
        handlerCallback4: null,
        handlerCallback5: null,
        handlerCallback6: null,
        handlerCallback7: null,
        handlerCallback8: null,
        UTF8ToString: function(arg) {
            console.log("UTF8ToString called with arg:", arg);
            return (typeof Pointer_stringify === 'undefined') ? UTF8ToString(arg) : Pointer_stringify(arg);
        },
        invokeCallback: function(args, callback) {
            console.log("invokeCallback called with args:", args);
            var sig = 'v';
            var messages = [];
            for (var i = 0; i < args.length; i++) {
                var message = args[i];
                var bufferSize = lengthBytesUTF8(message) + 1;
                var buffer = _malloc(bufferSize);
                stringToUTF8(message, buffer, bufferSize);
                messages.push(buffer);
                sig += 'i';
                console.log("Processed argument", i, ":", message, "buffer:", buffer);
            }
            console.log("Callback signature:", sig);
            if (typeof Runtime === 'undefined') {
                dynCall(sig, callback, messages);
                console.log("Callback invoked using dynCall");
            } else {
                Runtime.dynCall(sig, callback, messages);
                console.log("Callback invoked using Runtime.dynCall");
            }
        },
        handlers: []
    },

    InitJs: function(url) {
        console.log("InitJs called with url pointer:", url);
        url = vars.UTF8ToString(url);
        console.log("InitJs converted url:", url);
        if (vars.connection) {
            console.log("Existing connection found, stopping connection");
            vars.connection.stop().catch(function(err) {
                console.error("Error stopping connection:", err.toString());
            });
            vars.connection = null;
            vars.handlers = [];
            vars.lastConnectionId = '';
            vars.connectedCallback = null;
            vars.disconnectedCallback = null;
        }
        console.log("Creating new SignalR connection");
        vars.connection = new signalR.HubConnectionBuilder()
            .withUrl(url)
            .withAutomaticReconnect()
            .build();
        console.log("New SignalR connection created");
    },

    ConnectJs: function(connectedCallback, disconnectedCallback) {
        console.log("ConnectJs called");
        if (vars.connection) {
            console.log("Clearing existing connection event handlers");
            vars.connection.onclose(null);
            vars.connection.onreconnecting(null);
            vars.connection.onreconnected(null);
        }
        vars.connectedCallback = connectedCallback;
        vars.disconnectedCallback = disconnectedCallback;
        console.log("Starting SignalR connection");
        vars.connection.start()
            .then(function() {
                vars.lastConnectionId = vars.connection.connectionId;
                console.log("Connection started with id:", vars.lastConnectionId);
                vars.connection.onclose(function(err) {
                    console.log("Connection onclose triggered");
                    while (vars.handlers.length > 0) {
                        const handler = vars.handlers.pop();
                        console.log("Removing handler for method:", handler.methodName);
                        if (handler && handler.methodName) {
                            vars.connection.off(handler.methodName, handler);
                        }
                    }
                    if (err) {
                        console.error("Connection closed due to error:", err.toString());
                    }
                    if (vars.disconnectedCallback) {
                        console.log("Invoking disconnectedCallback with connection id:", vars.lastConnectionId);
                        vars.invokeCallback([vars.lastConnectionId], vars.disconnectedCallback);
                    }
                });
                vars.connection.onreconnecting(function(err) {
                    console.log("Connection onreconnecting triggered with error:", err.toString());
                });
                vars.connection.onreconnected(function(connectionId) {
                    console.log("Connection onreconnected triggered with new id:", connectionId);
                    vars.lastConnectionId = connectionId;
                    if (vars.connectedCallback) {
                        console.log("Invoking connectedCallback with new connection id:", vars.lastConnectionId);
                        vars.invokeCallback([vars.lastConnectionId], vars.connectedCallback);
                    }
                });
                if (vars.connectedCallback) {
                    console.log("Invoking connectedCallback with connection id:", vars.lastConnectionId);
                    vars.invokeCallback([vars.lastConnectionId], vars.connectedCallback);
                }
            })
            .catch(function(err) {
                console.error("Error starting connection:", err.toString());
                vars.connection = null;
            });
    },

    StopJs: function() {
        console.log("StopJs called");
        if (vars.connection) {
            console.log("Removing all handlers before stopping connection");
            while (vars.handlers.length > 0) {
                const handler = vars.handlers.pop();
                console.log("Removing handler for method:", handler.methodName);
                if (handler && handler.methodName) {
                    vars.connection.off(handler.methodName, handler);
                }
            }
            vars.connection.onclose(null);
            vars.connection.onreconnecting(null);
            vars.connection.onreconnected(null);
            console.log("Stopping SignalR connection");
            vars.connection.stop()
                .then(function() {
                    console.log("SignalR connection stopped successfully");
                    vars.connection = null;
                    vars.lastConnectionId = '';
                    vars.connectedCallback = null;
                    vars.disconnectedCallback = null;
                })
                .catch(function(err) {
                    console.error("Error stopping connection:", err.toString());
                    vars.connection = null;
                });
        } else {
            console.log("StopJs called but no active connection found");
        }
    },

    InvokeJs: function(methodName, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10) {
        console.log("InvokeJs called");
        if (!vars.connection) {
            console.error("No active SignalR connection");
            return;
        }
        methodName = vars.UTF8ToString(methodName);
        console.log("InvokeJs method name:", methodName);
        const args = [];
        const argumentsArray = [arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10];
        for (let i = 0; i < argumentsArray.length; i++) {
            if (argumentsArray[i]) {
                const convertedArg = vars.UTF8ToString(argumentsArray[i]);
                args.push(convertedArg);
                console.log("InvokeJs argument", i, "converted to:", convertedArg);
            } else {
                break;
            }
        }
        console.log("Invoking method on connection:", methodName, "with args:", args);
        vars.connection.invoke(methodName, ...args)
            .catch(function(err) {
                console.error("Error invoking method:", methodName, "Error:", err.toString());
            });
    },

    OnJs: function(methodName, argCount, callback) {
        console.log("OnJs called");
        if (!vars.connection) {
            console.error("No active SignalR connection");
            return;
        }
        methodName = vars.UTF8ToString(methodName);
        console.log("OnJs method name converted:", methodName);
        argCount = Number.parseInt(vars.UTF8ToString(argCount));
        console.log("OnJs argument count:", argCount);
        const createHandler = (args) => {
            console.log("Creating handler for method:", methodName, "with expected arg count:", args);
            const handler = (...handlerArgs) => {
                console.log("Handler for method", methodName, "triggered with args:", handlerArgs);
                if (vars.connection) {
                    try {
                        vars.invokeCallback([methodName, ...handlerArgs.map(arg => arg.toString())], callback);
                        console.log("Callback invoked for handler of method:", methodName);
                    } catch (err) {
                        console.error("Error in handler for method:", methodName, "Error:", err.toString());
                    }
                }
            };
            handler.methodName = methodName;
            vars.handlers.push(handler);
            console.log("Registering handler for method:", methodName);
            vars.connection.on(methodName, handler);
            return handler;
        };
        try {
            switch(argCount) {
                case 1:
                    return createHandler(1);
                case 2:
                    return createHandler(2);
                case 3:
                    return createHandler(3);
                case 4:
                    return createHandler(4);
                case 5:
                    return createHandler(5);
                case 6:
                    return createHandler(6);
                case 7:
                    return createHandler(7);
                case 8:
                    return createHandler(8);
                default:
                    throw new Error("Unsupported number of arguments: " + argCount);
            }
        } catch (err) {
            console.error("Error setting up handler for method:", methodName, "Error:", err.toString());
        }
    },
};

autoAddDeps(SignalRLib, '$vars');
mergeInto(LibraryManager.library, SignalRLib);
