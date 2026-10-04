#pragma once
#include "VEOImgSoLGlobal.h"
#include <string>
#include <cstring>
#include <exception>

typedef void EventCallback(EVENTDATA* _Data);

enum LOGEVENT_OPTION
{
	SDK_LOGEVENT = 0x00,
};

class VwSolutionSDKEventLog
{
public:
	VwSolutionSDKEventLog(EventCallback *EventCallback, void* pUserPointer)
	{
		EventCallbackPointer = EventCallback;
		m_pUserPointer = pUserPointer;
		m_EvtData._pParent = m_pUserPointer;
	}

	virtual ~VwSolutionSDKEventLog(void) {}	

	void Addlog(std::string Message, LOGEVENT_OPTION LogIndex)
	{
		m_EvtData._LOGINDEX = LogIndex;
		strncpy(CharMessage, Message.c_str(), (Message.size() < sizeof(CharMessage)) ? Message.size() : sizeof(CharMessage) - 1);
		CharMessage[(Message.size() < sizeof(CharMessage)) ? Message.size() : sizeof(CharMessage) - 1] = '\0';
		m_EvtData._MESSAGESIZE = static_cast<int>((Message.size() < sizeof(CharMessage)) ? Message.size() : sizeof(CharMessage) - 1);
		m_EvtData._MESSAGELOG = CharMessage;
		EventCallback_dll(&m_EvtData);
	}

	EventCallback* EventCallbackPointer;   // C# Callback Pointer
	void* m_pUserPointer;

private:
	EVENTDATA m_EvtData;
	char CharMessage[1024];
	
	void EventCallback_dll(EVENTDATA* _Data)
	{
		try
		{
			if (EventCallbackPointer != NULL)
				(EventCallbackPointer)(_Data);
		}
		catch (const std::exception &e)
		{
		}
	}
};
