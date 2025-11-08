#!/bin/bash
# APEX V2 Quick Validation Script

echo "🔍 APEX V2 Quick Issue Detection"
echo "================================"

PROJECT_PATH="c:\Users\Gaming\APEXv2\ApexV2"
cd "$PROJECT_PATH"

echo "📦 Checking build status..."
dotnet build --configuration Release --verbosity quiet > build_output.txt 2>&1
BUILD_EXIT_CODE=$?

echo "🔍 Analyzing issues..."

# Count compilation issues
ERRORS=$(grep -i "error" build_output.txt | wc -l)
WARNINGS=$(grep -i "warning" build_output.txt | wc -l)

# Check for common code issues
TODO_COUNT=$(find . -name "*.cs" -not -path "./bin/*" -not -path "./obj/*" | xargs grep -i "TODO\|FIXME\|HACK" | wc -l)
ASYNC_VOID=$(find . -name "*.cs" -not -path "./bin/*" -not -path "./obj/*" | xargs grep "async void" | wc -l)
EMPTY_CATCH=$(find . -name "*.cs" -not -path "./bin/*" -not -path "./obj/*" | xargs grep -A1 "catch.*{" | grep -B1 "^\s*}" | wc -l)

# Check XAML issues
XAML_MISSING_RESOURCES=$(find . -name "*.xaml" | xargs grep 'Source="/' | grep -v "pack://" | wc -l)

echo ""
echo "📊 VALIDATION RESULTS"
echo "===================="

if [ $BUILD_EXIT_CODE -eq 0 ]; then
    echo "✅ Build: SUCCESS"
else
    echo "❌ Build: FAILED"
fi

echo "🔴 Compilation Errors: $ERRORS"
echo "🟡 Compilation Warnings: $WARNINGS"
echo "🔵 TODO/FIXME Comments: $TODO_COUNT"
echo "🟠 Async Void Methods: $ASYNC_VOID"
echo "🟠 Empty Catch Blocks: $EMPTY_CATCH"
echo "🟡 Missing XAML Resources: $XAML_MISSING_RESOURCES"

TOTAL_ISSUES=$((ERRORS + ASYNC_VOID + EMPTY_CATCH + XAML_MISSING_RESOURCES))

echo ""
echo "📈 SUMMARY"
echo "=========="
echo "Total Critical Issues: $TOTAL_ISSUES"

if [ $TOTAL_ISSUES -eq 0 ] && [ $BUILD_EXIT_CODE -eq 0 ]; then
    echo "🎉 EXCELLENT! No critical issues found!"
else
    echo "⚠️  Issues found that need attention:"
    
    if [ $BUILD_EXIT_CODE -ne 0 ]; then
        echo "   🔥 PRIORITY 1: Fix compilation errors first"
        echo "      See build_output.txt for details"
    fi
    
    if [ $ASYNC_VOID -gt 0 ]; then
        echo "   🔥 PRIORITY 2: Fix async void methods ($ASYNC_VOID found)"
        echo "      Change async void to async Task"
    fi
    
    if [ $EMPTY_CATCH -gt 0 ]; then
        echo "   🔥 PRIORITY 3: Fix empty catch blocks ($EMPTY_CATCH found)"
        echo "      Add proper error handling"
    fi
    
    if [ $XAML_MISSING_RESOURCES -gt 0 ]; then
        echo "   🔧 Fix missing XAML resources ($XAML_MISSING_RESOURCES found)"
    fi
fi

echo ""
echo "💡 Next steps:"
echo "   1. Address critical issues in order of priority"
echo "   2. Run this script again after fixes"
echo "   3. Consider adding unit tests for fixed components"

# Show build errors if any
if [ $BUILD_EXIT_CODE -ne 0 ]; then
    echo ""
    echo "🔍 BUILD ERROR DETAILS:"
    echo "======================"
    head -20 build_output.txt
fi
