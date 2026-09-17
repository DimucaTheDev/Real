import React, { useState, useRef, useEffect } from 'react';
import { Terminal, Trash2, Search, ArrowDownCircle } from 'lucide-react';
import { SerilogEntry } from '../types/rhi';

interface ConsoleLogsProps {
  logs: SerilogEntry[];
  onClear: () => void;
}

export const ConsoleLogs: React.FC<ConsoleLogsProps> = ({ logs, onClear }) => {
  const [filterLevel, setFilterLevel] = useState<string>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [autoScroll, setAutoScroll] = useState(true);
  const bottomRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (autoScroll && bottomRef.current) {
      bottomRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  }, [logs, autoScroll]);

  const filtered = logs.filter((log) => {
    if (filterLevel !== 'ALL' && log.level !== filterLevel) return false;
    if (searchQuery.trim() && !log.message.toLowerCase().includes(searchQuery.toLowerCase())) return false;
    return true;
  });

  const getLevelBadge = (level: SerilogEntry['level']) => {
    switch (level) {
      case 'VRB':
        return <span className="text-neutral-500 font-bold">[VRB]</span>;
      case 'DBG':
        return <span className="text-blue-400 font-bold">[DBG]</span>;
      case 'INF':
        return <span className="text-emerald-400 font-bold">[INF]</span>;
      case 'WRN':
        return <span className="text-amber-400 font-bold">[WRN]</span>;
      case 'ERR':
        return <span className="text-red-400 font-bold">[ERR]</span>;
    }
  };

  return (
    <div className="h-44 bg-neutral-950 border-t border-neutral-800 flex flex-col font-mono text-xs select-none">
      {/* Console Top Bar */}
      <div className="px-3 py-1.5 bg-neutral-900 border-b border-neutral-800 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Terminal className="w-3.5 h-3.5 text-neutral-400" />
          <span className="text-[11px] font-semibold text-neutral-300 uppercase tracking-wider">
            Serilog Engine Diagnostics
          </span>
          <span className="text-[10px] text-neutral-500">({filtered.length} entries)</span>
        </div>

        <div className="flex items-center gap-2">
          {/* Level Filter */}
          <div className="flex items-center bg-neutral-950 rounded px-1 border border-neutral-800 text-[10px]">
            {['ALL', 'VRB', 'DBG', 'INF', 'WRN'].map((lvl) => (
              <button
                key={lvl}
                onClick={() => setFilterLevel(lvl)}
                className={`px-1.5 py-0.5 rounded transition-colors ${
                  filterLevel === lvl
                    ? 'text-neutral-100 font-bold bg-neutral-800'
                    : 'text-neutral-500 hover:text-neutral-300'
                }`}
              >
                {lvl}
              </button>
            ))}
          </div>

          {/* Search */}
          <div className="relative">
            <Search className="w-3 h-3 absolute left-1.5 top-1.5 text-neutral-500" />
            <input
              type="text"
              placeholder="Filter logs..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="bg-neutral-950 border border-neutral-800 rounded pl-5 pr-2 py-0.5 text-[11px] text-neutral-300 focus:outline-none focus:border-red-500 w-28 md:w-36"
            />
          </div>

          <button
            onClick={onClear}
            className="p-1 rounded text-neutral-500 hover:text-red-400 transition-colors"
            title="Clear Console"
          >
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Logs Output Container */}
      <div className="flex-1 overflow-y-auto p-2.5 space-y-1 select-text bg-neutral-950/90 leading-tight">
        {filtered.map((log) => (
          <div key={log.id} className="flex items-start gap-2 hover:bg-neutral-900/40 px-1 py-0.5 rounded">
            <span className="text-neutral-600 shrink-0 text-[10px]">{log.timestamp}</span>
            <span className="shrink-0 text-[10px]">{getLevelBadge(log.level)}</span>
            <span className="text-neutral-500 shrink-0 text-[10px]">[{log.source}]</span>
            <span className="text-neutral-300 flex-1 break-words">{log.message}</span>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>
    </div>
  );
};
