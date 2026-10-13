import React, { useMemo, useState } from 'react';
import { Bot, MessageSquareText, Send, Sparkles, X } from 'lucide-react';
import { AnalysisResult, SubscriptionInfo } from '../types';

interface DataChatPanelProps {
  isOpen: boolean;
  onClose: () => void;
  analysis: AnalysisResult | null;
  subscription: SubscriptionInfo | null;
  foundryConfigured: boolean;
}

interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

const suggestedPrompts = [
  'Summarize the biggest savings opportunities',
  'Which recommendation should I prioritize first?',
  'Explain the risk profile of this portfolio'
];

export const DataChatPanel: React.FC<DataChatPanelProps> = ({
  isOpen,
  onClose,
  analysis,
  subscription,
  foundryConfigured
}) => {
  const [draft, setDraft] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>([]);

  const contextSummary = useMemo(() => {
    if (!analysis) {
      return 'Run a subscription scan to attach portfolio context to this conversation.';
    }

    return `${analysis.recommendations.length} recommendations · ${analysis.totalResourcesScanned} resources · ${analysis.totalNetRealizableSavingsMonthly.toLocaleString('en-US', {
      style: 'currency',
      currency: 'USD'
    })}/month realizable savings`;
  }, [analysis]);

  if (!isOpen) return null;

  const submitMessage = () => {
    const message = draft.trim();
    if (!message || !analysis) return;

    setMessages((current) => [
      ...current,
      { role: 'user', content: message },
      {
        role: 'assistant',
        content: 'The current-data chat surface is ready. Conversational responses will be enabled when the chat API is connected; recommendation details and Foundry explanations remain available in the dashboard today.'
      }
    ]);
    setDraft('');
  };

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <button
        type="button"
        aria-label="Close data chat"
        onClick={onClose}
        className="absolute inset-0 bg-slate-950/35"
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="data-chat-title"
        className="relative flex h-full w-full max-w-md flex-col border-l border-slate-200 bg-white shadow-2xl"
      >
        <header className="border-b border-slate-200 bg-slate-900 px-5 py-4 text-white">
          <div className="flex items-start justify-between gap-4">
            <div className="flex items-start gap-3">
              <div className="rounded-xl bg-indigo-500/20 p-2.5">
                <MessageSquareText className="h-5 w-5 text-indigo-300" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h2 id="data-chat-title" className="font-bold">Chat about this data</h2>
                  <span className="rounded-full border border-amber-400/30 bg-amber-400/10 px-2 py-0.5 text-[10px] font-semibold text-amber-200">
                    Preview shell
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-400">
                  {subscription?.displayName ?? 'No subscription selected'}
                </p>
              </div>
            </div>
            <button
              type="button"
              aria-label="Close chat panel"
              onClick={onClose}
              className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        </header>

        <div className="border-b border-indigo-100 bg-indigo-50 px-5 py-4">
          <div className="flex items-center gap-2 text-xs font-bold text-indigo-900">
            <Sparkles className="h-4 w-4 text-indigo-600" />
            Current analysis context
          </div>
          <p className="mt-1.5 text-xs leading-5 text-indigo-800">{contextSummary}</p>
          <p className="mt-2 text-[11px] font-medium text-indigo-700">
            {foundryConfigured
              ? 'Azure AI Foundry is configured for future conversational responses.'
              : 'Deterministic context is available; Foundry configuration is currently unavailable.'}
          </p>
        </div>

        <div className="flex-1 space-y-4 overflow-y-auto px-5 py-5">
          <div className="flex gap-3">
            <div className="mt-0.5 rounded-full bg-indigo-100 p-2">
              <Bot className="h-4 w-4 text-indigo-700" />
            </div>
            <div className="rounded-2xl rounded-tl-sm bg-slate-100 px-4 py-3 text-sm leading-6 text-slate-700">
              Ask questions about the active subscription’s savings, recommendation mix, evidence, or risk. This preview establishes the experience and carries the current scan context.
            </div>
          </div>

          {analysis && messages.length === 0 && (
            <div className="space-y-2">
              <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400">Try asking</p>
              {suggestedPrompts.map((prompt) => (
                <button
                  type="button"
                  key={prompt}
                  onClick={() => setDraft(prompt)}
                  className="block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-left text-xs font-medium text-slate-700 hover:border-indigo-300 hover:bg-indigo-50"
                >
                  {prompt}
                </button>
              ))}
            </div>
          )}

          {messages.map((message, index) => (
            <div
              key={`${message.role}-${index}`}
              className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}
            >
              <div
                className={`max-w-[85%] rounded-2xl px-4 py-3 text-sm leading-6 ${
                  message.role === 'user'
                    ? 'rounded-br-sm bg-indigo-600 text-white'
                    : 'rounded-tl-sm bg-slate-100 text-slate-700'
                }`}
              >
                {message.content}
              </div>
            </div>
          ))}
        </div>

        <footer className="border-t border-slate-200 p-4">
          <div className="flex items-end gap-2">
            <textarea
              aria-label="Chat message"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              disabled={!analysis}
              placeholder={analysis ? 'Ask about the current analysis…' : 'Run a scan to enable data chat'}
              rows={2}
              className="min-h-12 flex-1 resize-none rounded-xl border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200 disabled:bg-slate-100"
            />
            <button
              type="button"
              aria-label="Send chat message"
              onClick={submitMessage}
              disabled={!analysis || !draft.trim()}
              className="rounded-xl bg-indigo-600 p-3 text-white shadow-sm hover:bg-indigo-500 disabled:cursor-not-allowed disabled:bg-slate-300"
            >
              <Send className="h-4 w-4" />
            </button>
          </div>
          <p className="mt-2 text-center text-[10px] text-slate-400">
            Preview only — no conversational API call is made yet.
          </p>
        </footer>
      </aside>
    </div>
  );
};
