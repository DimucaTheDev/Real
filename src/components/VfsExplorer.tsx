import React, { useState } from 'react';
import { Folder, File, HardDrive, Package, Binary, Plus, Save, RefreshCw } from 'lucide-react';
import { engineVfs, VfsFile } from '../engine/vfs';
import { fmodEngine } from '../engine/audio';

interface VfsExplorerProps {
  onLogMessage: (msg: string, level?: 'INF' | 'VRB' | 'DBG' | 'WRN') => void;
}

export const VfsExplorer: React.FC<VfsExplorerProps> = ({ onLogMessage }) => {
  const [files, setFiles] = useState<VfsFile[]>(engineVfs.listFiles());
  const [selectedFile, setSelectedFile] = useState<VfsFile | null>(files[0] || null);
  const [editContent, setEditContent] = useState(selectedFile?.content || '');
  const [activeSchemeFilter, setActiveSchemeFilter] = useState<'all' | 'file' | 'pak' | 'assembly'>('all');

  const [newFilePath, setNewFilePath] = useState('');
  const [newFileScheme, setNewFileScheme] = useState<'file' | 'pak' | 'assembly'>('file');

  const handleSelectFile = (file: VfsFile) => {
    setSelectedFile(file);
    setEditContent(file.content);
    fmodEngine.playSoundEvent('click');
    onLogMessage(`VFS: OpenRead("${file.path}")`, 'VRB');
  };

  const handleSaveContent = () => {
    if (!selectedFile) return;
    engineVfs.setFileContent(selectedFile.path, editContent);
    setFiles(engineVfs.listFiles());
    fmodEngine.playSoundEvent('pipeline_compile');
    onLogMessage(`VFS: WriteAllText("${selectedFile.path}") [${editContent.length} chars]`, 'INF');
  };

  const handleCreateFile = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newFilePath.trim()) return;

    const fullPath = `${newFileScheme}://${newFilePath.trim().replace(/^[\\/]+/, '')}`;
    engineVfs.registerFile(fullPath, `// RealEngine resource\n`, false);
    const updated = engineVfs.listFiles();
    setFiles(updated);
    const created = engineVfs.getFile(fullPath);
    if (created) {
      setSelectedFile(created);
      setEditContent(created.content);
    }
    setNewFilePath('');
    fmodEngine.playSoundEvent('pipeline_compile');
    onLogMessage(`VFS: Registered new file "${fullPath}"`, 'INF');
  };

  const filteredFiles = activeSchemeFilter === 'all'
    ? files
    : files.filter((f) => f.scheme === activeSchemeFilter);

  return (
    <div className="flex-1 flex flex-col md:flex-row h-full overflow-hidden bg-neutral-950">
      {/* File Tree Sidebar */}
      <div className="w-full md:w-80 border-b md:border-b-0 md:border-r border-neutral-800 flex flex-col bg-neutral-900">
        {/* VFS Schemes Header & Filter */}
        <div className="p-3 border-b border-neutral-800 space-y-2">
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-neutral-300 flex items-center gap-1.5">
              <HardDrive className="w-3.5 h-3.5 text-red-400" />
              Real.Core.Assets.Vfs
            </h3>
            <span className="text-[10px] text-neutral-500 font-mono">{files.length} mounted</span>
          </div>

          <div className="grid grid-cols-4 gap-1 text-[11px] font-mono">
            {(['all', 'file', 'pak', 'assembly'] as const).map((scheme) => (
              <button
                key={scheme}
                onClick={() => setActiveSchemeFilter(scheme)}
                className={`py-0.5 rounded text-center transition-colors ${
                  activeSchemeFilter === scheme
                    ? 'bg-neutral-800 text-neutral-100 border border-neutral-700 font-semibold'
                    : 'text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {scheme}
              </button>
            ))}
          </div>
        </div>

        {/* Files List */}
        <div className="flex-1 overflow-y-auto p-2 space-y-1">
          {filteredFiles.map((file) => {
            const isSelected = selectedFile?.path === file.path;
            const schemeColor =
              file.scheme === 'file'
                ? 'text-blue-400'
                : file.scheme === 'pak'
                ? 'text-amber-400'
                : 'text-emerald-400';

            return (
              <button
                key={file.path}
                onClick={() => handleSelectFile(file)}
                className={`w-full text-left p-2 rounded-lg text-xs font-mono flex items-start gap-2 transition-all ${
                  isSelected
                    ? 'bg-neutral-800 text-neutral-100 border border-neutral-700 shadow-sm'
                    : 'text-neutral-400 hover:bg-neutral-800/50 hover:text-neutral-200'
                }`}
              >
                {file.isBinary ? (
                  <Binary className={`w-3.5 h-3.5 mt-0.5 shrink-0 ${schemeColor}`} />
                ) : (
                  <File className={`w-3.5 h-3.5 mt-0.5 shrink-0 ${schemeColor}`} />
                )}
                <div className="truncate flex-1">
                  <div className="truncate font-medium">{file.path}</div>
                  <div className="text-[10px] text-neutral-500 flex items-center gap-2 mt-0.5">
                    <span>{file.sizeBytes} B</span>
                    <span>&bull;</span>
                    <span>{file.isBinary ? 'Binary' : 'UTF-8 Text'}</span>
                  </div>
                </div>
              </button>
            );
          })}
        </div>

        {/* Add File Form */}
        <form onSubmit={handleCreateFile} className="p-2 border-t border-neutral-800 bg-neutral-950 flex flex-col gap-1.5">
          <div className="flex items-center gap-1.5">
            <select
              value={newFileScheme}
              onChange={(e) => setNewFileScheme(e.target.value as 'file' | 'pak' | 'assembly')}
              className="bg-neutral-900 border border-neutral-700 text-[11px] rounded px-1.5 py-1 text-neutral-200 font-mono"
            >
              <option value="file">file://</option>
              <option value="pak">pak://</option>
              <option value="assembly">assembly://</option>
            </select>
            <input
              type="text"
              placeholder="path/file.ext"
              value={newFilePath}
              onChange={(e) => setNewFilePath(e.target.value)}
              className="flex-1 bg-neutral-900 border border-neutral-700 text-[11px] rounded px-2 py-1 text-neutral-200 font-mono focus:outline-none focus:border-red-500"
            />
            <button
              type="submit"
              className="bg-red-600 hover:bg-red-500 text-white p-1 rounded transition-colors"
              title="Add File to VFS"
            >
              <Plus className="w-3.5 h-3.5" />
            </button>
          </div>
        </form>
      </div>

      {/* File Inspector / Editor */}
      <div className="flex-1 flex flex-col bg-neutral-950">
        {selectedFile ? (
          <>
            <div className="p-3 border-b border-neutral-800 bg-neutral-900 flex items-center justify-between">
              <div className="flex items-center gap-2 text-xs font-mono">
                <span className="text-neutral-400">ResourceLocation:</span>
                <span className="text-neutral-100 font-bold">{selectedFile.path}</span>
                <span className="text-[10px] px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-400 border border-neutral-700">
                  {selectedFile.scheme} provider
                </span>
              </div>

              {!selectedFile.isBinary && (
                <button
                  onClick={handleSaveContent}
                  className="flex items-center gap-1 bg-neutral-800 hover:bg-neutral-700 text-neutral-200 text-xs px-2.5 py-1 rounded font-mono transition-colors border border-neutral-700"
                >
                  <Save className="w-3.5 h-3.5 text-emerald-400" />
                  Save to VFS
                </button>
              )}
            </div>

            <div className="flex-1 p-3 overflow-auto">
              {selectedFile.isBinary ? (
                <div className="space-y-3 font-mono text-xs">
                  <div className="p-3 rounded-lg bg-neutral-900/60 border border-neutral-800 text-neutral-400">
                    Binary asset: Hex header preview (ReadBytes offset: 0, count: 64)
                  </div>
                  <pre className="p-4 rounded-lg bg-neutral-900 border border-neutral-800 text-amber-300 font-mono text-xs leading-relaxed overflow-x-auto select-text">
                    {`00000000: 03 02 23 07 00 00 01 00  0b 00 08 00 20 00 00 00  ..#......... ...
00000010: 00 00 00 00 11 00 02 00  01 00 00 00 0b 00 06 00  ................
00000020: 01 00 00 00 47 4c 53 4c  2e 73 74 64 2e 34 35 30  ....GLSL.std.450
00000030: 0e 00 03 00 00 00 00 00  01 00 00 00 0f 00 07 00  ................`}
                  </pre>
                  <div className="text-[11px] text-neutral-500">
                    Content descriptor: {selectedFile.content}
                  </div>
                </div>
              ) : (
                <textarea
                  value={editContent}
                  onChange={(e) => setEditContent(e.target.value)}
                  className="w-full h-full bg-neutral-950 text-neutral-200 p-3 rounded-lg border border-neutral-800/80 focus:outline-none focus:border-red-500 font-mono text-xs resize-none leading-relaxed"
                  spellCheck={false}
                />
              )}
            </div>
          </>
        ) : (
          <div className="flex-1 flex items-center justify-center text-xs text-neutral-500 font-mono">
            Select a file from VFS to inspect or edit
          </div>
        )}
      </div>
    </div>
  );
};
