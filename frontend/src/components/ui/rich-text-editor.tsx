'use client';

import React, { useRef, useEffect, useCallback, forwardRef, useImperativeHandle } from 'react';
import { cn } from '../../lib/utils';
import { Button } from './button';
import { Separator } from './separator';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './select';
import { Input } from './input';
import {
  Bold,
  Italic,
  Underline,
  Strikethrough,
  AlignLeft,
  AlignCenter,
  AlignRight,
  AlignJustify,
  List,
  ListOrdered,
  Link,
  Image,
  Table,
  Type,
  Palette,
  Highlighter,
  Superscript,
  Subscript,
  Quote,
  Code,
  Hash,
  Minus
} from 'lucide-react';

export interface RichTextEditorRef {
  insertText: (text: string) => void;
  getContent: () => string;
  setContent: (content: string) => void;
  focus: () => void;
}

interface RichTextEditorProps {
  value?: string;
  onChange?: (content: string) => void;
  className?: string;
  placeholder?: string;
  minHeight?: string;
}

export const RichTextEditor = forwardRef<RichTextEditorRef, RichTextEditorProps>(
  ({ value = '', onChange, className, placeholder, minHeight = '500px' }, ref) => {
    const editorRef = useRef<HTMLDivElement>(null);
    const isUpdatingRef = useRef(false);
    const lastSelectionRef = useRef<Range | null>(null);
    const [currentFontFamily, setCurrentFontFamily] = React.useState('system-ui');
    const [currentFontSize, setCurrentFontSize] = React.useState('14px');
    const [showColorPicker, setShowColorPicker] = React.useState(false);

    // Save and restore cursor position
    const saveCursorPosition = useCallback(() => {
      const selection = window.getSelection();
      if (!selection || !editorRef.current || selection.rangeCount === 0) return null;

      const range = selection.getRangeAt(0);
      const preCaretRange = range.cloneRange();
      preCaretRange.selectNodeContents(editorRef.current);
      preCaretRange.setEnd(range.startContainer, range.startOffset);
      return preCaretRange.toString().length;
    }, []);

    const restoreCursorPosition = useCallback((position: number) => {
      if (!editorRef.current) return;

      const selection = window.getSelection();
      if (!selection) return;

      const walker = document.createTreeWalker(
        editorRef.current,
        NodeFilter.SHOW_TEXT,
        null
      );

      let currentPosition = 0;
      let node: Node | null = null;

      while ((node = walker.nextNode())) {
        const textNode = node as Text;
        const textLength = textNode.textContent?.length || 0;

        if (currentPosition + textLength >= position) {
          const range = document.createRange();
          range.setStart(textNode, position - currentPosition);
          range.collapse(true);
          selection.removeAllRanges();
          selection.addRange(range);
          return;
        }

        currentPosition += textLength;
      }

      // If we couldn't find the exact position, place cursor at the end
      const range = document.createRange();
      range.selectNodeContents(editorRef.current);
      range.collapse(false);
      selection.removeAllRanges();
      selection.addRange(range);
    }, []);

    // Save current selection when editor loses focus
    const saveSelection = useCallback(() => {
      const selection = window.getSelection();
      if (selection && selection.rangeCount > 0 && editorRef.current) {
        const range = selection.getRangeAt(0);
        // Only save if the selection is within our editor
        if (editorRef.current.contains(range.commonAncestorContainer)) {
          lastSelectionRef.current = range.cloneRange();
        }
      }
    }, []);

    // Restore saved selection
    const restoreSelection = useCallback(() => {
      if (lastSelectionRef.current && editorRef.current) {
        try {
          const selection = window.getSelection();
          if (selection) {
            selection.removeAllRanges();
            selection.addRange(lastSelectionRef.current.cloneRange());
          }
        } catch (error) {
          // If restoring fails, place cursor at end
          const selection = window.getSelection();
          if (selection && editorRef.current) {
            const range = document.createRange();
            range.selectNodeContents(editorRef.current);
            range.collapse(false);
            selection.removeAllRanges();
            selection.addRange(range);
          }
        }
      }
    }, []);

    // Handle input changes
    const handleInput = useCallback(() => {
      if (!editorRef.current || isUpdatingRef.current) return;

      const content = editorRef.current.innerHTML;
      saveSelection(); // Save selection after input
      onChange?.(content);
    }, [onChange, saveSelection]);

    // Handle editor commands
    const executeCommand = useCallback((command: string, value?: string) => {
      if (!editorRef.current) return;

      editorRef.current.focus();
      const cursorPosition = saveCursorPosition();
      
      document.execCommand(command, false, value);
      
      // Small delay to ensure command is executed
      setTimeout(() => {
        if (cursorPosition !== null) {
          restoreCursorPosition(cursorPosition);
        }
        handleInput();
      }, 0);
    }, [saveCursorPosition, restoreCursorPosition, handleInput]);

    // Insert text at cursor position
    const insertText = useCallback((text: string) => {
      if (!editorRef.current) return;

      // Focus the editor first
      editorRef.current.focus();
      
      // Use a longer timeout to ensure focus is established
      setTimeout(() => {
        if (!editorRef.current) return;
        
        let range: Range | null = null;
        const selection = window.getSelection();
        
        // Try to use the saved selection first
        if (lastSelectionRef.current) {
          try {
            range = lastSelectionRef.current.cloneRange();
          } catch (error) {
            console.log('Saved selection invalid, using current selection');
            range = null;
          }
        }
        
        // If no saved selection, try current selection
        if (!range && selection && selection.rangeCount > 0) {
          const currentRange = selection.getRangeAt(0);
          // Ensure the selection is within our editor
          if (editorRef.current.contains(currentRange.commonAncestorContainer)) {
            range = currentRange;
          }
        }
        
        // Fallback: create range at end of content
        if (!range) {
          range = document.createRange();
          range.selectNodeContents(editorRef.current);
          range.collapse(false);
        }
        
        // Clear any selected content
        range.deleteContents();
        
        // Create and insert the text node
        const textNode = document.createTextNode(text);
        range.insertNode(textNode);
        
        // Position cursor after the inserted text
        const newRange = document.createRange();
        newRange.setStartAfter(textNode);
        newRange.collapse(true);
        
        // Apply the new selection
        if (selection) {
          selection.removeAllRanges();
          selection.addRange(newRange);
          // Save this new selection for future use
          lastSelectionRef.current = newRange.cloneRange();
        }
        
        handleInput();
      }, 50); // Increased timeout for better reliability
    }, [handleInput]);

    // Set content programmatically
    const setContent = useCallback((content: string) => {
      if (!editorRef.current) return;
      
      isUpdatingRef.current = true;
      editorRef.current.innerHTML = content;
      isUpdatingRef.current = false;
    }, []);

    // Get current content
    const getContent = useCallback(() => {
      return editorRef.current?.innerHTML || '';
    }, []);

    // Focus the editor
    const focusEditor = useCallback(() => {
      editorRef.current?.focus();
    }, []);

    // Expose methods via ref
    useImperativeHandle(ref, () => ({
      insertText,
      getContent,
      setContent,
      focus: focusEditor,
    }), [insertText, getContent, setContent, focusEditor]);

    // Initialize content
    useEffect(() => {
      if (!editorRef.current || isUpdatingRef.current) return;
      
      const currentContent = editorRef.current.innerHTML;
      if (currentContent !== value) {
        setContent(value);
      }
    }, [value, setContent]);

    // Handle keyboard shortcuts
    const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey) {
        switch (e.key) {
          case 'b':
            e.preventDefault();
            executeCommand('bold');
            break;
          case 'i':
            e.preventDefault();
            executeCommand('italic');
            break;
          case 'u':
            e.preventDefault();
            executeCommand('underline');
            break;
        }
      }
    }, [executeCommand]);

    // Advanced editor commands
    const insertLink = useCallback(() => {
      const url = prompt('Enter URL:');
      if (url) {
        executeCommand('createLink', url);
      }
    }, [executeCommand]);

    const insertImage = useCallback(() => {
      const url = prompt('Enter image URL:');
      if (url) {
        executeCommand('insertImage', url);
      }
    }, [executeCommand]);

    const formatFont = useCallback((fontFamily: string) => {
      executeCommand('fontName', fontFamily);
      setCurrentFontFamily(fontFamily);
    }, [executeCommand]);

    const formatFontSize = useCallback((fontSize: string) => {
      executeCommand('fontSize', fontSize);
      setCurrentFontSize(fontSize);
    }, [executeCommand]);

    const formatTextColor = useCallback((color: string) => {
      executeCommand('foreColor', color);
    }, [executeCommand]);

    const formatBackgroundColor = useCallback((color: string) => {
      executeCommand('backColor', color);
    }, [executeCommand]);

    const insertTable = useCallback(() => {
      const rows = prompt('Number of rows:', '2');
      const cols = prompt('Number of columns:', '2');
      if (rows && cols) {
        let tableHTML = '<table border="1" style="border-collapse: collapse; width: 100%;">';
        for (let r = 0; r < parseInt(rows); r++) {
          tableHTML += '<tr>';
          for (let c = 0; c < parseInt(cols); c++) {
            tableHTML += '<td style="padding: 8px; border: 1px solid #ccc;">Cell</td>';
          }
          tableHTML += '</tr>';
        }
        tableHTML += '</table><p><br></p>';
        executeCommand('insertHTML', tableHTML);
      }
    }, [executeCommand]);

    // Handle paste to clean up formatting
    const handlePaste = useCallback((e: React.ClipboardEvent) => {
      e.preventDefault();
      
      const text = e.clipboardData.getData('text/plain');
      insertText(text);
    }, [insertText]);

    return (
      <div className="border border-input rounded-md">
        {/* Enhanced Toolbar */}
        <div className="border-b p-2 space-y-2">
          {/* First Row - Font and Size */}
          <div className="flex flex-wrap items-center gap-2">
            <Select value={currentFontFamily} onValueChange={formatFont}>
              <SelectTrigger className="w-[140px] h-8">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="system-ui">System UI</SelectItem>
                <SelectItem value="Arial">Arial</SelectItem>
                <SelectItem value="Georgia">Georgia</SelectItem>
                <SelectItem value="Times New Roman">Times New Roman</SelectItem>
                <SelectItem value="Courier New">Courier New</SelectItem>
                <SelectItem value="Helvetica">Helvetica</SelectItem>
                <SelectItem value="Verdana">Verdana</SelectItem>
              </SelectContent>
            </Select>
            
            <Select value={currentFontSize} onValueChange={formatFontSize}>
              <SelectTrigger className="w-[70px] h-8">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="10px">10px</SelectItem>
                <SelectItem value="12px">12px</SelectItem>
                <SelectItem value="14px">14px</SelectItem>
                <SelectItem value="16px">16px</SelectItem>
                <SelectItem value="18px">18px</SelectItem>
                <SelectItem value="20px">20px</SelectItem>
                <SelectItem value="24px">24px</SelectItem>
                <SelectItem value="32px">32px</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Second Row - Text Formatting */}
          <div className="flex flex-wrap items-center gap-1">
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('bold')}>
              <Bold className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('italic')}>
              <Italic className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('underline')}>
              <Underline className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('strikethrough')}>
              <Strikethrough className="h-4 w-4" />
            </Button>
            
            <Separator orientation="vertical" className="h-6" />
            
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('superscript')}>
              <Superscript className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('subscript')}>
              <Subscript className="h-4 w-4" />
            </Button>
            
            <Separator orientation="vertical" className="h-6" />
            
            {/* Color Controls */}
            <div className="relative">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => formatTextColor('#000000')}
              >
                <Type className="h-4 w-4" />
              </Button>
            </div>
            <div className="relative">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => formatBackgroundColor('#ffff00')}
              >
                <Highlighter className="h-4 w-4" />
              </Button>
            </div>
            
            <Separator orientation="vertical" className="h-6" />
            
            {/* Alignment */}
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('justifyLeft')}>
              <AlignLeft className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('justifyCenter')}>
              <AlignCenter className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('justifyRight')}>
              <AlignRight className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('justifyFull')}>
              <AlignJustify className="h-4 w-4" />
            </Button>
            
            <Separator orientation="vertical" className="h-6" />
            
            {/* Lists */}
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('insertUnorderedList')}>
              <List className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('insertOrderedList')}>
              <ListOrdered className="h-4 w-4" />
            </Button>
            
            <Separator orientation="vertical" className="h-6" />
            
            {/* Advanced Features */}
            <Button type="button" variant="ghost" size="sm" onClick={insertLink}>
              <Link className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={insertImage}>
              <Image className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={insertTable}>
              <Table className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('formatBlock', 'blockquote')}>
              <Quote className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('formatBlock', 'pre')}>
              <Code className="h-4 w-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => executeCommand('insertHorizontalRule')}>
              <Minus className="h-4 w-4" />
            </Button>
          </div>
        </div>
        
        {/* Editor */}
        <div
          ref={editorRef}
          contentEditable
          className={cn(
            "p-4 focus:outline-none prose prose-sm max-w-none",
            className
          )}
          style={{
            minHeight,
            whiteSpace: 'pre-wrap',
            lineHeight: '1.6'
          }}
          onInput={handleInput}
          onKeyDown={handleKeyDown}
          onKeyUp={saveSelection}
          onMouseUp={saveSelection}
          onClick={saveSelection}
          onBlur={saveSelection}
          onFocus={restoreSelection}
          onPaste={handlePaste}
          suppressContentEditableWarning={true}
          data-placeholder={placeholder}
        />
      </div>
    );
  }
);

RichTextEditor.displayName = 'RichTextEditor';